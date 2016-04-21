import std.string;
import std.typecons;
import std.algorithm.sorting;

string 
neg(string literal) pure 
{ 
  if (startsWith(literal, "~"))
    return literal[1..$];
  else
    return "~" ~ literal; 
}

bool
isAssumption(string literal) pure
{
  return startsWith(literal, "@") || startsWith(literal, "~@");
}

string
literalString(string literal) pure
{
  if (literal.indexOf(" ") >= 0 || literal.indexOf("->") >= 0) {
    if (literal[0] == '~')
      return  "~\"" ~ literal[1..$] ~ "\"";
    else 
      return "\"" ~ literal ~ "\"";
  } 
  else 
    return literal;
}

//string
//subscriptChar(char c) = (char)(0x2080 + ((int)c - (int)'0'))

// Return a string with the decimal value of x in subscript.
//let rec subscript x = if x < 0 then "\u208b" + subscript -x else String.map subscriptChar (x.ToString())

// TODO: Move these to a utility file.
bool
isSortedSet(T)(const T[] array) pure
{
  if (!isSorted(array))
    return false;
  
  auto iPrevious = 0;
  for (auto i = 1; i < array.length; ++i) {
    if (array[i] == array[iPrevious])
      return false;

    iPrevious = i;
  }

  return true;
}

bool equals(string a, string b) pure { return a == b; }
bool equals(T)(const T a, const T b) pure { return a.equals(b); }

/** Modify the array to remove repeated elements and return the new
 * length of the array.
 */
int
removeRepeats(T)(T[] array) pure
{
  if (array.length <= 1)
    // Nothing to do.
    return array.length;
  
  auto iPrevious = 0;
  auto iTo = 1;
  for (auto iFrom = 1; iFrom < array.length; ++iFrom) {
    // Use our own equals to get around "Cannot call impure opEquals".
    if (equals(array[iFrom], array[iPrevious]))
      continue;
    
    if (iTo != iFrom)
      array[iTo] = array[iFrom];

    iPrevious = iTo;
    ++iTo;
  }
  
  return iTo;
}

class Rule {
  this(string consequent, immutable string[] antecedents) pure immutable
  {
    this.consequent = consequent;
    if (isSortedSet(antecedents))
      this.antecedents = antecedents;
    else {
      auto newAntecedents = antecedents.dup;
      sort(newAntecedents);
      newAntecedents.length = removeRepeats(newAntecedents);
      this.antecedents = cast(immutable string[])newAntecedents;
    }
  }
  
  this(string consequent, string antecedent) pure immutable
  {
    this.consequent = consequent;
    this.antecedents = [antecedent];
  }
  
  override string 
  toString() pure const
  {
    auto result = "";
    foreach (antecedent; antecedents) {
      if (result != "")
        result ~= ", ";
      result ~= literalString(antecedent);
    }

    return result ~ " -> " ~ literalString(consequent);
  }

  int
  opCmp(const Rule other) @safe const pure nothrow
  {
    if (consequent < other.consequent)
      return -1;
    if (consequent > other.consequent)
      return 1;
    
    if (antecedents < other.antecedents)
      return -1;
    if (antecedents > other.antecedents)
      return 1;
    return 0;
  }

  bool equals(const Rule other) const pure
  {
    return consequent == other.consequent && antecedents == other.antecedents; 
  }

  override bool opEquals(Object o) pure
  {
    auto other = cast(Rule)o;
    if (other is null)
      return false;
    return equals(other);
  }

  Rebindable!(immutable(Rule))[]
  transpositions() pure const
  {
    auto result = new Rebindable!(immutable(Rule))[antecedents.length];
    for (auto i = 0; i < antecedents.length; ++i) {
      auto newAntecedents = antecedents.dup;
      newAntecedents[i] = neg(consequent);
      sort(newAntecedents);

      result[i] = new immutable Rule(neg(antecedents[i]), cast(immutable string[])newAntecedents);
    }

    sort(result);
    result.length = removeRepeats(result);
    return result;
  }
  
  string consequent;
  string[] antecedents;
}

Rebindable!(immutable(Rule))[]
transitiveClosure(const Rule[] rules) pure
{
  auto result = new Rebindable!(immutable(Rule))[0];
  foreach (rule; rules) {
    foreach (x; rule.transpositions())
      result ~= x;
  }

  sort(result);
  result.length = removeRepeats(result);
  return result;
}
