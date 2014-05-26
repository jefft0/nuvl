module ArgueLib.Argue

let neg(literal:string) = if literal.StartsWith("~") then literal.Substring(1) else "~" + literal
let literalString(literal:string) = if literal.Contains(" ") then "\"" + literal + "\"" else literal
let subscriptChar (c:char) = (char)(0x2080 + ((int)c - (int)'0'))
// Return a string with the decimal value of x in subscript.
let rec subscript x = if x < 0 then "\u208b" + subscript -x else String.map subscriptChar (x.ToString())
let setOfArray array = Set.ofArray array

type PropositionType = AXIOM | ASSUMPTION
type Proposition = 
  { Name: string; PropositionType: PropositionType }
  static member make(name, propositionType) = {Name = name; PropositionType = propositionType}
  static member makeAssumption(name) = {Name = name; PropositionType = ASSUMPTION}
  static member makeAxiom(name) = {Name = name; PropositionType = AXIOM}
  override this.ToString() = literalString this.Name

type Rule = 
  { Consequent: string ; Antecedents: Set<string> }
  static member make(consequent, antecedent) = {Consequent = consequent ; Antecedents = Set.singleton antecedent}

  override this.ToString() = 
    (Set.fold (fun acc antecedent -> acc + ", " + literalString antecedent) "" this.Antecedents).Substring(2) +
     " -> " + literalString this.Consequent

let transpositions { Antecedents = antecedents; Consequent = consequent } =
  Set.map (fun antecedent -> 
           { Antecedents = antecedents.Remove(antecedent).Add(neg consequent); Consequent = neg antecedent }) antecedents

let transitiveClosure rules = Set.fold (fun acc rule -> transpositions rule + acc) rules rules

let rec nonFlatRule assumptions rules = 
  match rules with
  | rule :: tail -> if Set.contains rule.Consequent assumptions then Some rule else nonFlatRule assumptions tail
  | [] -> None

let ensuredFlat assumptions rules = 
  match nonFlatRule assumptions (Set.toList rules) with
  | Some rule -> raise(System.ArgumentException("An assumption is the consequent of a rule " + rule.ToString()))
  | _ -> rules

type Argument = 
  { TopRule: Rule option; SubArguments: Set<Argument>; Conclusion: string; Rules: Set<Rule>; Premises: Set<Proposition>; 
    DirectSubArguments: Set<Argument> }
  static member make(topRule, subArguments) = 
    let rules = Set.fold (fun rules arg -> match arg.TopRule with Some r -> Set.add r rules | _ -> rules) 
                         (Set.singleton topRule) subArguments
    let premises = Set.fold (fun premises arg -> premises + arg.Premises) Set.empty subArguments
    { TopRule = Some topRule; SubArguments = subArguments; Conclusion = topRule.Consequent; Rules = rules; Premises = premises; 
      // Populate the direct sub-arguments - those arguments whose conclusions are the antecedents of the top rule.
      DirectSubArguments = Set.filter (fun arg -> topRule.Antecedents.Contains arg.Conclusion) subArguments } 
  // From a single premise p.
  static member make p = 
    { TopRule = None; SubArguments = Set.empty; Conclusion = p.Name; Rules = Set.empty; Premises = Set.singleton p; 
      DirectSubArguments = Set.empty }
  member this.isFirm() = Set.forall (fun p -> p.PropositionType = AXIOM) this.Premises

type Attack = { From: int; To: int }

type ArgumentationTheory =
  { ArgumentsByIndex: Map<int, Argument> }
  // Compute ArgumentsByIndex from arguments.
  static member make arguments =
    { ArgumentsByIndex = Set.fold (fun map arg -> map.Add(map.Count, arg)) Map.empty arguments }
  member this.getArgumentIndexesByConclusion c = 
    Map.fold (fun indexes i arg -> if arg.Conclusion = c then Set.add i indexes else indexes) Set.empty this.ArgumentsByIndex
  member this.indexOf argument = Map.findKey (fun _ arg -> arg = argument) this.ArgumentsByIndex
  member this.argumentIndexes() = Map.fold (fun indexes key _ -> Set.add key indexes) Set.empty this.ArgumentsByIndex
  member this.toString argument =
    match argument.TopRule with
    | Some _ -> "A" + (this.indexOf argument).ToString() + ": " +
                 (Set.fold (fun subArgList subArg -> 
                             subArgList + ", " + literalString subArg.Conclusion + subscript (this.indexOf subArg)) 
                           "" argument.DirectSubArguments).Substring(2) +
                 " -> " + literalString argument.Conclusion
    | _ -> "A" + (this.indexOf argument).ToString() + ": " + literalString argument.Conclusion

let rec cartesianSubProduct index sets =
  if index = Array.length sets then
    Set.singleton []
  else
    let subProduct = cartesianSubProduct (index + 1) sets
    Set.fold (fun ret obj -> Set.fold (fun ret list -> ret.Add (obj :: list)) ret subProduct) Set.empty sets.[index]

let cartesianProduct sets = cartesianSubProduct 0 sets

let argsForAntecedent args r =
  // Loop through the antecedents of the rule.
  Set.fold (fun antFF ant -> 
      // Loop through the arguments.
      Set.fold (fun antFF a -> 
         // So if this argument already contains this rule, we can't create a new arg.
         if not (a.Rules.Contains r) && a.Conclusion = ant then
           let mapArgs = match Map.tryFind ant antFF with Some x -> x | _ -> Set.singleton a
           antFF.Add(ant, mapArgs.Add a)
         else
           // Don't add.
           antFF) 
        antFF args) 
    Map.empty r.Antecedents

let rec constructArgumentsHelper args rules =
  let saveArgsCount = Set.count args

  let newArgs = 
    Set.fold (fun args r -> 
        let antFF = argsForAntecedent args r
        let keys = Map.fold (fun keys key _ -> Set.add key keys) Set.empty antFF

        if keys.IsSupersetOf r.Antecedents then
          let antSets = Map.fold (fun antSets _ value -> value :: antSets) [] antFF

          let antSets2 = cartesianProduct (List.toArray antSets)
          Set.fold (fun args l ->
              let allArgRulesDontHaveR = List.forall (fun arg -> not (arg.Rules.Contains r)) l
              let selfSupport = not allArgRulesDontHaveR
              if not selfSupport then
                args.Add(Argument.make(r, (Set.ofList l)))
              else
                args)
            args antSets2
        else
          args)
      args rules

  if newArgs.Count = saveArgsCount then newArgs else constructArgumentsHelper newArgs rules

let constructArguments propositions rules =
  // Simple to begin with with -- add all the atomic propositions.
  let args = Set.fold (fun args p -> Set.add (Argument.make p) args) Set.empty propositions

  // Call the recursive method to obtain complex args.
  constructArgumentsHelper args rules

let calculateAttack (theory:ArgumentationTheory) =
  Map.fold (fun attack key argument -> 
      if argument.TopRule.IsNone && not (argument.isFirm()) then
        Set.fold (fun attack a2 -> Set.add {From = a2; To = key} attack) 
          attack (theory.getArgumentIndexesByConclusion(neg argument.Conclusion))
      else
        attack) 
    Set.empty theory.ArgumentsByIndex

let calculateDefeat theory =
  let attack = calculateAttack theory

  // Loop through all the attacks.
  let tempDefeat = 
    Set.fold (fun tempDefeatIn att ->
        let tempDefeat = Set.add att tempDefeatIn
        // If this argument is defeated, defeat any arguments in which it's a sub-argument.
        Map.fold (fun tempDefeat a3Key a3Arg -> 
            if a3Arg.SubArguments.Contains theory.ArgumentsByIndex.[att.To] then 
              Set.add {From = att.From; To = a3Key} tempDefeat 
            else 
              tempDefeat)
          tempDefeat theory.ArgumentsByIndex)
      Set.empty attack

  Set.fold (fun tempDefeat2 d -> 
      Map.fold (fun tempDefeat2 aKey aArg -> 
          if aArg.SubArguments.Contains theory.ArgumentsByIndex.[d.To] then
            Set.add {From = d.From; To = aKey} tempDefeat2
          else
            tempDefeat2)
        tempDefeat2 theory.ArgumentsByIndex) 
    tempDefeat tempDefeat

let rec getGroundedExtHelper args activeAtts groundedExtIn =
  // eligibleArgs is the arguments that aren't yet in groundedExt.
  let eligibleArgs0 = args - groundedExtIn
  // Remove everything in eligibleArgs that's attacked by non-defeated attackers.
  let eligibleArgs = Set.fold (fun eligibleArgs tempAtt -> Set.remove tempAtt.To eligibleArgs) eligibleArgs0 activeAtts

  if eligibleArgs.IsEmpty then
    // If everything is attacked, the extension can't be further expanded, so done. 
    groundedExtIn
  else
    // Otherwise add everything that isn't attacked to the extension.
    let groundedExt = groundedExtIn + eligibleArgs

    // defeated is the arguments that are attacked by groundedExt.
    // Remove all attacks whose attackers are defeated by arguments in the extension.
    let defeated = 
      Set.fold (fun defeated attack -> 
          if (eligibleArgs.Contains attack.From) then Set.add attack.To defeated else defeated) 
        Set.empty activeAtts

    let activeAtts2 = Set.filter (fun attack -> not (defeated.Contains attack.From)) activeAtts
    getGroundedExtHelper args activeAtts2 groundedExt

let getGroundedExt args atts = getGroundedExtHelper args  atts Set.empty
