/*
Copyright (C) 2017 Jeff Thompson

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

package org.nuvl.argue

import scala.collection.JavaConversions
import scala.collection.mutable

import org.nuvl.argue.aba_plus.ABA_Plus
import org.nuvl.argue.aba_plus.ASPARTIX_Interface
import org.nuvl.argue.aba_plus.Rule
import org.nuvl.argue.aba_plus.Sentence

final class NuvlFramework(assumptions: Set[Sentence], baseRules: Set[Rule]) {
  import NuvlFramework._

  val rules = mutable.Set[Rule]()
  rules ++= baseRules

  // Add rule transpositions.
  for (rule <- baseRules) {
    // The transposition of [a1, a2] -> b is
    // [a2, b.contrary] -> a1.contrary and [a1, b.contrary] -> a2.contrary.
    for (a <- rule.antecedent) {
      val newAntecedent = (rule.antecedent - a) + rule.consequent.contrary
      rules += Rule(newAntecedent, a.contrary)
    }
  }

  // Add rules for [@a] -> a , and its transposition.
  for (assumption <- assumptions) {
    rules += Rule(Set(atAssumption(assumption)), assumption)
    rules += Rule(Set(assumption.contrary), atAssumption(assumption).contrary)
  }

  val aba = new ABA_Plus(assumptions.map(atAssumption), Set(), rules.toSet)

  val preferredExtensions = new ASPARTIX_Interface(aba).calculate_preferred_extensions
  // The grounded extension is the intersection of the preferred extensions.
  val groundedExtension = preferredExtensions.foldLeft(
    preferredExtensions.head)(_ & _)

  def this(assumptions: java.util.HashSet[Sentence], 
           baseRules: java.util.HashSet[Rule]) =
    this(JavaConversions.asScalaSet(assumptions).toSet,
         JavaConversions.asScalaSet(baseRules).toSet)
}

object NuvlFramework {
  // TODO: What if the assumption is a contrary?
  def atAssumption(assumption: Sentence) = Sentence("@" + assumption.symbol)
}
