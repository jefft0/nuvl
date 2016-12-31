package org.nuvl.argue

object Rule {
  type Literal = String

  type Rule = (Set[Literal], Literal)

  def make(body: Set[Literal], head: Literal): Rule = (body, head)
  def make(body: Literal, head: Literal): Rule = (Set(body), head)
}
