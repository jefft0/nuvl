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

object AttackType extends Enumeration {
  val NORMAL_ATK, REVERSE_ATK = Value
}

/**
 * Create an Attack between Deductions.
 * https://github.com/zb95/2016-ABAPlus/blob/4b189ec939d3033dd5100c20ce2fde2f94ad51ae/aba_plus_.py#L495
 * @param attacker A Deduction whose conclusion is the contrary of the premise 
 * of the attackee.
 * @param attackee A Deduction whose premise is the contrary of the conclusion 
 * of the attacker.
 * @param type AttackType.NORMAL_ATK or AttackType.REVERSE_ATK.
 */
case class Attack(attacker: Deduction, attackee: Deduction, typ: AttackType.Value)
