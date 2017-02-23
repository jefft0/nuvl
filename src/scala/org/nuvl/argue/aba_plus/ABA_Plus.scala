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

/**
 * BA_Plus that represents an ABA+ framework and its components (assumptions,
 * rules and preferences).
 * https://github.com/zb95/2016-ABAPlus/blob/4b189ec939d3033dd5100c20ce2fde2f94ad51ae/aba_plus_.py#L32
 */
case class ABA_Plus
  (assumptions: Set[Sentence], preferences: Set[Preference], rules: Set[Rule]) {

  // TODO: check_or_auto_WCP

  /**
   * Check if the ABA+ framework is flat.
   * @return True if framework is flat, false otherwise.
   */
  def is_flat() = !rules.exists(assumptions contains _.consequent)
}
