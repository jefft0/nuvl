module ArgueLib.Argue

let neg(literal:string) = if literal.StartsWith("~") then literal.Substring(1) else "~" + literal
let literalString(literal:string) = if (literal.IndexOf " ") >= 0 || (literal.IndexOf "->") >= 0 then 
                                      "\"" + literal + "\"" else literal
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
  static member toString(proposition) = literalString proposition.Name
  member this.toString() = Proposition.toString(this)

type Rule = 
  { Consequent: string; Antecedents: Set<string> }
  static member make(consequent, antecedents) = {Consequent = consequent; Antecedents = antecedents}
  static member make(consequent, antecedent) = {Consequent = consequent; Antecedents = Set.singleton antecedent}
  static member toString(rule) = 
    (Set.fold (fun acc antecedent -> acc + ", " + literalString antecedent) "" rule.Antecedents).Substring(2) +
     " -> " + literalString rule.Consequent
  member this.toString() = Rule.toString(this)

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
  { TopRule: Rule option; QuasiSubArguments: Set<Argument>; Conclusion: string; Rules: Set<Rule>; Premises: Set<Proposition>; 
    DirectSubArguments: Set<Argument> }
  static member make(topRule, subArguments) = 
    let rules = Set.fold (fun rules arg -> match arg.TopRule with Some r -> Set.add r rules | _ -> rules) 
                         (Set.singleton topRule) subArguments
    let premises = Set.fold (fun premises arg -> premises + arg.Premises) Set.empty subArguments
    { TopRule = Some topRule; QuasiSubArguments = subArguments; Conclusion = topRule.Consequent; Rules = rules; Premises = premises; 
      // Populate the direct sub-arguments - those arguments whose conclusions are the antecedents of the top rule.
      DirectSubArguments = Set.filter (fun arg -> topRule.Antecedents.Contains arg.Conclusion) subArguments } 
  // From a single premise p.
  static member make p = 
    { TopRule = None; QuasiSubArguments = Set.empty; Conclusion = p.Name; Rules = Set.empty; Premises = Set.singleton p; 
      DirectSubArguments = Set.empty }
  member this.isFirm() = Set.forall (fun p -> p.PropositionType = AXIOM) this.Premises
  member this.getSubArguments() = this.QuasiSubArguments // Set.fold (fun temp a -> a.DirectSubArguments + temp) Set.empty this.DirectSubArguments

type Attack = { From: int; To: int }

type ArgumentationTheory =
  { Arguments: Argument[]; ArgumentIndexes: Set<int>; ArgumentIndexesByConclusion: Map<string, int[]>}
  // Compute Arguments from an argument set.
  static member make argumentSet =
    let arguments = Set.toArray argumentSet
    let argumentIndexes = Set.ofArray [|0 .. arguments.Length - 1|]
    { Arguments = arguments; ArgumentIndexes = argumentIndexes;
      ArgumentIndexesByConclusion = Set.fold (fun map i -> 
          let arg = arguments.[i]
          match Map.tryFind arg.Conclusion map with 
          | Some array -> map.Add(arg.Conclusion, Array.append array [|i|]) 
          | _ -> map.Add(arg.Conclusion, [|i|])) 
        Map.empty argumentIndexes }
  member this.getArgumentIndexesByConclusion c = match Map.tryFind c this.ArgumentIndexesByConclusion with Some a -> a | _ -> [||]
  member this.indexOf argument = Array.findIndex ((=) argument) this.Arguments
  member this.conclusionString i = 
    let conclusion = this.Arguments.[i].Conclusion
    let indexesForConclusion = this.ArgumentIndexesByConclusion.[conclusion]
    if indexesForConclusion.Length = 1 then 
      literalString conclusion
    else 
      literalString conclusion + subscript (1 + Array.findIndex ((=) i) indexesForConclusion)
  member this.toString i =
    let argument = this.Arguments.[i]
    match argument.TopRule with
    | Some _ -> (Set.fold (fun subArgList subArg -> subArgList + ", " + this.conclusionString (this.indexOf subArg)) 
                   "" argument.DirectSubArguments).Substring(2) + " -> " + this.conclusionString i
    | _ -> this.conclusionString i

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
  Set.fold (fun attack a1Index ->
      let a1 = theory.Arguments.[a1Index]
      if a1.TopRule.IsNone && not (a1.isFirm()) then
        Array.fold (fun attack a2 -> Set.add {From = a2; To = a1Index} attack) 
          attack (theory.getArgumentIndexesByConclusion(neg a1.Conclusion))
      else
        attack) 
    Set.empty theory.ArgumentIndexes

let calculateDefeat theory =
  let attack = calculateAttack theory

  // Loop through all the attacks.
  let tempDefeat = 
    Set.fold (fun tempDefeatIn att ->
        let tempDefeat = Set.add att tempDefeatIn
        // If this argument is defeated, defeat any arguments in which it's a sub-argument.
        Set.fold (fun tempDefeat a3Index ->
            let a3 = theory.Arguments.[a3Index] 
            if a3.getSubArguments().Contains theory.Arguments.[att.To] then 
              Set.add {From = att.From; To = a3Index} tempDefeat 
            else 
              tempDefeat)
          tempDefeat theory.ArgumentIndexes)
      Set.empty attack

  Set.fold (fun tempDefeat2 d -> 
      Set.fold (fun tempDefeat2 aIndex  ->
          let a = theory.Arguments.[aIndex]
          if a.getSubArguments().Contains theory.Arguments.[d.To] then
            Set.add {From = d.From; To = aIndex} tempDefeat2
          else
            tempDefeat2)
        tempDefeat2 theory.ArgumentIndexes) 
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

let getGroundedExt args atts = getGroundedExtHelper args atts Set.empty
