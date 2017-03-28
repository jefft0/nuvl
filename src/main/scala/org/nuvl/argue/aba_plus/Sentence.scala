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

// https://github.com/zb95/2016-ABAPlus/blob/4b189ec939d3033dd5100c20ce2fde2f94ad51ae/aba_plus_.py#L450
case class Sentence(symbol: String, is_contrary: Boolean = false) {
  def this(symbol: String, is_contrary: java.lang.Boolean) =
    this(symbol, is_contrary.booleanValue)

  def contrary() = Sentence(symbol, !is_contrary)
}
