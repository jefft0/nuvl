module ArgueLib.Argue

let neg(literal:string) = if literal.StartsWith("~") then literal.Substring(1) else "~" + literal
let literalString(literal:string) = if literal.Contains(" ") then "\"" + literal + "\"" else literal
let subscriptChar (c:char) = (char)(0x2080 + ((int)c - (int)'0'))
// Return a string with the decimal value of x in subscript.
let rec subscript x = if x < 0 then "\u208b" + subscript -x else String.map subscriptChar (x.ToString())
let setOfArray array = Set.ofArray array

type PropositionType = AXIOM | PREMISE | ASSUMPTION
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
  | None -> rules

type Argument = 
  { TopRule: Rule option; SubArguments: Set<Argument>; Conclusion: string; Rules: Set<Rule>; Premises: Set<Proposition>; 
    DirectSubArguments: Set<Argument> }
  static member make(topRule, subArguments) = 
    let rules = Set.fold (fun rules arg -> match arg.TopRule with Some r -> Set.add r rules | None -> rules) 
                         (Set.singleton topRule) subArguments
    let premises = Set.fold (fun premises arg -> premises + arg.Premises) Set.empty subArguments
    { TopRule = Some topRule; SubArguments = subArguments; Conclusion = topRule.Consequent; Rules = rules; Premises = premises; 
      // Populate the direct sub-arguments - those arguments whose conclusions are the antecedents of the top rule.
      DirectSubArguments = Set.filter (fun arg -> topRule.Antecedents.Contains arg.Conclusion) subArguments } 
  // From a single premise p.
  static member make p = 
    { TopRule = None; SubArguments = Set.empty; Conclusion = p.Name; Rules = Set.empty; Premises = Set.singleton p; 
      DirectSubArguments = Set.empty }
  static member argumentIds(arguments) =
    Set.fold (fun ids argument -> Map.add argument (ids.Count + 1) ids) Map.empty arguments
  member this.toString argumentIds =
    match this.TopRule with
    | Some _ -> "A" + (Map.find this argumentIds).ToString() + ": " +
                 (Set.fold (fun subArgList subArg -> 
                             subArgList + ", " + literalString subArg.Conclusion + subscript (Map.find subArg argumentIds)) 
                           "" this.DirectSubArguments).Substring(2) +
                 " -> " + literalString this.Conclusion
    | None -> "A" + (Map.find this argumentIds).ToString() + ": " + literalString this.Conclusion

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
           let mapArgs = match Map.tryFind ant antFF with Some x -> x | None -> Set.singleton a
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

// f x y = y |> f x = y |> (x |> f) = x |> f <| y
// f x (g y) = (g y) |> f x = (g y) |> (x |> f) = x |> f <| (g y)
// (g >> f) x = (f << g) x = f (g x)
// (f >> (>>) g) x y = g >> (f x) y = f x (g y)
