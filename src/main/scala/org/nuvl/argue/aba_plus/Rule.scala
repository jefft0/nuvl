/*
Copyright (C) 2017 Jeff Thompson
From https://github.com/zb95/2016-ABAPlus/blob/master/aba_plus_.py
by Ziyi Bao, Department of Computing, Imperial College London

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

package org.nuvl.argue.aba_plus

// https://github.com/zb95/2016-ABAPlus/blob/4b189ec939d3033dd5100c20ce2fde2f94ad51ae/aba_plus_.py#L430
case class Rule(antecedent: Set[Sentence], consequent: Sentence) {
  def this(consequent: Sentence) = this(Set[Sentence](), consequent)
  def this(antecedent1: Sentence, consequent: Sentence) =
    this(Set[Sentence](antecedent1), consequent)
  def this(antecedent1: Sentence, antecedent2: Sentence, consequent: Sentence) =
    this(Set[Sentence](antecedent1, antecedent2), consequent)
  def this(antecedent1: Sentence, antecedent2: Sentence, antecedent3: Sentence,
           consequent: Sentence) =
    this(Set[Sentence](antecedent1, antecedent2, antecedent3), consequent)
  def this(antecedent1: Sentence, antecedent2: Sentence, antecedent3: Sentence,
           antecedent4: Sentence, consequent: Sentence) =
    this(Set[Sentence](antecedent1, antecedent2, antecedent3, antecedent4), consequent)
}
