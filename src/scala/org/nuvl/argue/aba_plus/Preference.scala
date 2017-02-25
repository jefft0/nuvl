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

sealed abstract class PreferenceRelation
case object LESS_THAN extends PreferenceRelation
case object LESS_EQUAL extends PreferenceRelation
case object NO_RELATION extends PreferenceRelation

/**
 * Create a Preference between two Sentences.
 * Example: Preference(a, b, LESS_THAN) represents a < b .
 * https://github.com/zb95/2016-ABAPlus/blob/4b189ec939d3033dd5100c20ce2fde2f94ad51ae/aba_plus_.py#L472
 * @param assump1 The first Sentence.
 * @param assump2 The second Sentence.
 * @param relation (optional) LESS_THAN, LESS_EQUAL or NO_RELATION. If omitted,
 * use NO_RELATION.
 */
case class Preference
    (assump1: Sentence, assump2: Sentence, 
     relation: PreferenceRelation = NO_RELATION)
