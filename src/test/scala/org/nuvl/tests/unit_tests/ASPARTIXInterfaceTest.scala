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

package org.nuvl.tests.unit_tests

import org.scalatest._
import org.junit.runner.RunWith
import org.scalatest.junit.JUnitRunner
import scala.collection.mutable
import org.nuvl.argue.aba_plus.ABA_Plus
import org.nuvl.argue.aba_plus.ASPARTIX_Interface
import org.nuvl.argue.aba_plus.Preference
import org.nuvl.argue.aba_plus.PreferenceRelation
import org.nuvl.argue.aba_plus.Rule
import org.nuvl.argue.aba_plus.Sentence
import org.scalatest.Assertions._

/**
 * ASPARTIXInterfaceTest runs the ABA+ unit test method TestASPARTIXInterface from
 * https://github.com/zb95/2016-ABAPlus/blob/555c83f02777ceab7688038e34466ea4563a837d/test.py
 * Note: This really should be an integration test since it uses the file system,
 * but leave it as a unit test for convenience.
 */
@RunWith(classOf[JUnitRunner])
class ASPARTIXInterfaceTest extends FunSuite with Matchers {
  test("test_simple_calculate_admissible_extensions") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(a, b)

    val rule = Rule(Set(a), b.contrary)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    val asp = new ASPARTIX_Interface(abap, "test.lp")

    val adm_ext = asp.calculate_admissible_extensions

    assert(adm_ext == Set(Set(a), Set()))
  }

  test("test_simple_calculate_stable_extensions1") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(a, b)

    val rule = Rule(Set(a), b.contrary)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    val asp = new ASPARTIX_Interface(abap, "test.lp")

    val stable_ext = asp.calculate_stable_extensions

    assert(stable_ext == Set(Set(a)))
  }

  test("test_calculate_stable_extensions_none_exists") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b, c)

    val rule1 = Rule(Set(a), b.contrary)
    val rule2 = Rule(Set(b), c.contrary)
    val rule3 = Rule(Set(c), a.contrary)
    val rules = Set(rule1, rule2, rule3)

    val pref = Preference(b, a, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test4.lp")

    val stable_ext = asp.calculate_stable_extensions

    assert(stable_ext == Set())
  }

  // Example 4 from aba+ unit tests.
  test("test_calculate_extensions") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val assumptions = Set(a, b, c, d)

    val rule1 = Rule(Set(a), b.contrary)
    val rule2 = Rule(Set(b), a.contrary)
    val rule3 = Rule(Set(a), c.contrary)
    val rule4 = Rule(Set(c), b.contrary)
    val rules = Set(rule1, rule2, rule3, rule4)

    val pref = Preference(c, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test_calculate_extensions.lp")

    val stable_ext = asp.calculate_stable_extensions
    assert(stable_ext == Set(Set(a, d), Set(b, d)))
  }

  // Example 6 from aba+ unit tests.
  test("test_calculate_extensions2") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b, c)

    val rule1 = Rule(Set(a, c), b.contrary)
    val rule2 = Rule(Set(b, c), a.contrary)
    val rule3 = Rule(Set(a, b), c.contrary)
    val rules = Set(rule1, rule2, rule3)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val pref2 = Preference(c, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1, pref2)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test_calculate_extensions2.lp")

    val stable_ext = asp.calculate_stable_extensions
    assert(stable_ext == Set(Set(a, b), Set(b, c)))

    val complete_ext = asp.calculate_complete_extensions
    assert(complete_ext == Set(Set(b), Set(a, b), Set(b, c)))

    val preferred_ext = asp.calculate_preferred_extensions
    assert(preferred_ext == Set(Set(a, b), Set(b, c)))

    val grounded_ext = asp.calculate_grounded_extensions
    assert(grounded_ext == Set(Set(b)))

/* TODO: Implement ideal.
    val ideal_ext = asp.calculate_ideal_extensions
    assert(ideal_ext == Set(Set(b)))
*/
  }

  // Example 7 from aba+ unit tests.
  test("test_calculate_extensions3") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b, c)

    val rule1 = Rule(Set(a, c), b.contrary)
    val rule2 = Rule(Set(b, c), a.contrary)
    val rule3 = Rule(Set(b), c.contrary)
    val rules = Set(rule1, rule2, rule3)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val pref2 = Preference(c, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1, pref2)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test_calculate_extensions3.lp")

    val stable_ext = asp.calculate_stable_extensions
    assert(stable_ext == Set(Set(a, b)))

    val complete_ext = asp.calculate_complete_extensions
    assert(complete_ext == Set(Set(a, b)))

    val preferred_ext = asp.calculate_preferred_extensions
    assert(preferred_ext == Set(Set(a, b)))

    val grounded_ext = asp.calculate_grounded_extensions
    assert(grounded_ext == Set(Set(a, b)))

/* TODO: Implement ideal.
    val ideal_ext = asp.calculate_ideal_extensions
    assert(ideal_ext == Set(Set(a, b)))
*/
  }

  // Example 8 from aba+ unit tests.
  test("test_calculate_extensions4") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b, c)

    val rule1 = Rule(Set(a, c), b.contrary)
    val rule2 = Rule(Set(b, c), a.contrary)
    val rules = Set(rule1, rule2)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val pref2 = Preference(b, c, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1, pref2)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test_calculate_extensions2.lp")

    val stable_ext = asp.calculate_stable_extensions
    assert(stable_ext == Set(Set(b, c)))

    val complete_ext = asp.calculate_complete_extensions
    assert(complete_ext == Set(Set(b, c)))

    val preferred_ext = asp.calculate_preferred_extensions
    assert(preferred_ext == Set(Set(b, c)))

    val grounded_ext = asp.calculate_grounded_extensions
    assert(grounded_ext == Set(Set(b, c)))

/* TODO: Implement ideal.
    val ideal_ext = asp.calculate_ideal_extensions
    assert(ideal_ext == Set(Set(b, c)))
*/
  }

  test("test_calculate_extensions5") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(a, b)

    val rule1 = Rule(Set(a), b.contrary)
    val rules = Set(rule1)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test_calculate_extensions5.lp")

    val stable_ext = asp.calculate_stable_extensions
    assert(stable_ext == Set(Set(b)))

    val complete_ext = asp.calculate_complete_extensions
    assert(complete_ext == Set(Set(b)))

    val preferred_ext = asp.calculate_preferred_extensions
    assert(preferred_ext == Set(Set(b)))

    val grounded_ext = asp.calculate_grounded_extensions
    assert(grounded_ext == Set(Set(b)))

/* TODO: Implement ideal.
    val ideal_ext = asp.calculate_ideal_extensions
    assert(ideal_ext == Set(Set(b)))
*/
  }

  test("test_calculate_extensions6") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val assumptions = Set(a, b, c, d)

    val rule1 = Rule(Set(a), b.contrary)
    val rule2 = Rule(Set(b, c), d.contrary)
    val rules = Set(rule1, rule2)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val asp = new ASPARTIX_Interface(abap, "test_calculate_extensions6.lp")

    val stable_ext = asp.calculate_stable_extensions
    assert(stable_ext == Set(Set(b, c)))

    val complete_ext = asp.calculate_complete_extensions
    assert(complete_ext == Set(Set(b, c)))

    val preferred_ext = asp.calculate_preferred_extensions
    assert(preferred_ext == Set(Set(b, c)))

    val grounded_ext = asp.calculate_grounded_extensions
    assert(grounded_ext == Set(Set(b, c)))

/* TODO: Implement ideal.
    val ideal_ext = asp.calculate_ideal_extensions
    assert(ideal_ext == Set(Set(b, c)))
*/
  }
}
