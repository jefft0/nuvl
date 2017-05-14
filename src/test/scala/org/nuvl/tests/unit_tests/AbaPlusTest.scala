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
import org.nuvl.argue.aba_plus.Attack
import org.nuvl.argue.aba_plus.AttackType
import org.nuvl.argue.aba_plus.Deduction
import org.nuvl.argue.aba_plus.Preference
import org.nuvl.argue.aba_plus.PreferenceRelation
import org.nuvl.argue.aba_plus.Rule
import org.nuvl.argue.aba_plus.Sentence
import org.scalatest.Assertions._

/**
 * AbaPlusTest runs the unit ABA+ unit tests from
 * https://github.com/zb95/2016-ABAPlus/blob/555c83f02777ceab7688038e34466ea4563a837d/test.py
 */
@RunWith(classOf[JUnitRunner])
class AbaPlusTest extends FunSuite with Matchers {
  test("test_simple_transitive_closure") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b, c)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val pref2 = Preference(b, c, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1, pref2)

    val abap = new ABA_Plus(assumptions, preferences, Set())
    assert(abap.preferences ==
           Set(pref1, pref2, Preference(a, c, PreferenceRelation.LESS_THAN)))
  }

  test("test_simple_deduction_exists") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(b)

    val rule = Rule(Set(b), a)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.deduction_exists(a, Set(b)))
  }

  test("test_deduction_from_empty_set_exists") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(b)

    val rule = Rule(Set(), a)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.deduction_exists(a, Set()))
  }

  test("test_simple_deduction_does_not_exist") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b)

    val rule = Rule(Set(b), c)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(!abap.deduction_exists(a, Set(b)))
  }

  test("test_transitive_deduction_exists") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(b)

    val rule1 = Rule(Set(b), c)
    val rule2 = Rule(Set(c), a)
    val rules = Set(rule1, rule2)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.deduction_exists(a, Set(b)))
  }

  test("test_transitive_deduction_from_empty_set_exists") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set[Sentence]()

    val rule1 = Rule(Set(b), a)
    val rule2 = Rule(Set(), b)
    val rules = Set(rule1, rule2)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.deduction_exists(a, Set()))
  }

  test("test_complex_deduction_exists") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val e = Sentence("e")
    val f = Sentence("f")
    val g = Sentence("g")
    val assumptions = Set(a, b, e)

    val rule1 = Rule(Set(a, b), c)
    val rule2 = Rule(Set(e), f)
    val rule3 = Rule(Set(c, f), g)
    val rules = Set(rule1, rule2, rule3)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.deduction_exists(g, Set(a, b, e)))
  }

  test("test_complex_deduction_does_not_exist") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val e = Sentence("e")
    val f = Sentence("f")
    val g = Sentence("g")
    val assumptions = Set(a, b, e)

    val rule1 = Rule(Set(a, b), c)
    val rule2 = Rule(Set(e), f)
    val rule3 = Rule(Set(c, f), g)
    val rules = Set(rule1, rule2, rule3)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(!abap.deduction_exists(g, Set(a, e)))
  }

  // TODO: disabled_test_simple_WCP_no_violation_check1
  // TODO: disabled_test_simple_WCP_no_violation_check2
  // TODO: disabled_test_simple_WCP_no_violation_check3
  // TODO: test_simple_WCP_violation_check1
  // TODO: test_simple_WCP_violation_check2
  // TODO: test_transitive_WCP_violation_check
  // TODO: test_transitive_WCP_violation_check
  // TODO: test_cycle_WCP_violattion_check
  // TODO: test_cycle_WCP_no_violation_check
  // TODO: test_complex_WCP_no_violation_check1
  // TODO: test_complex_WCP_no_violation_check2
  // TODO: test_check_and_partially_satisfy_WCP
  // TODO: test_check_and_partially_satisfy_WCP2

  test("test_set_combinations") {
    var set1 = mutable.Set[Set[String]]()
    set1 += Set("b")

    var set2 = mutable.Set[Set[String]]()
    set2 += Set("e")
    set2 += Set("f")

    var set3 = mutable.Set[Set[String]]()
    set3 += Set("g")

    var set4 = mutable.Set[Set[String]]()
    set4 += Set("i")
    set4 += Set("k")

    val combs = ABA_Plus.set_combinations(Set(set1, set2, set3, set4))
    val correct_combs = mutable.Set(Set("b", "e", "g", "i"), Set("b", "e", "g", "k"),
                                    Set("b", "f", "g", "i"), Set("b", "f", "g", "k"))
    assert(combs == correct_combs)
  }

  test("test_simple_generate_argument1") {
    val a = Sentence("a")
    val assumptions = Set(a)

    val abap = new ABA_Plus(assumptions, Set(), Set())

    assert(abap.generate_arguments(a) == Set(Set(a)))
  }

  test("test_simple_generate_argument2") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(b, c)

    val rule = Rule(Set(b, c), a)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(b, c)))
  }

  test("test_generate_empty_argument") {
    val a = Sentence("a")

    val rule = Rule(Set(), a)
    val rules = Set(rule)

    val abap = new ABA_Plus(Set(), Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set()))
  }

  test("test_transitive_generate_argument") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val e = Sentence("e")
    val assumptions = Set(b, c, d)

    val rule1 = Rule(Set(b, e), a)
    val rule2 = Rule(Set(c, d), e)
    val rules = Set(rule1, rule2)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(b, c, d)))
  }

  test("test_generate_multiple_arguments1") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val e = Sentence("e")
    val assumptions = Set(b, d, e)

    val rule1 = Rule(Set(b, c), a)
    val rule2 = Rule(Set(d), c)
    val rule3 = Rule(Set(e), c)
    val rules = Set(rule1, rule2, rule3)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(b, d), Set(b, e)))
  }

  test("test_generate_multiple_arguments2") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(b)

    val rule1 = Rule(Set(b), a)
    val rule2 = Rule(Set(), a)
    val rules = Set(rule1, rule2)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(), Set(b)))
  }

  test("test_generate_multiple_arguments3") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val assumptions = Set(b, d)

    val rule1 = Rule(Set(b, c), a)
    val rule2 = Rule(Set(), c)
    val rule3 = Rule(Set(d), c)
    val rules = Set(rule1, rule2, rule3)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(b), Set(b, d)))
  }

  test("test_cycle_generate_argument1") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set(b)

    val rule1 = Rule(Set(a), a)
    val rule2 = Rule(Set(b), a)
    val rules = Set(rule1, rule2)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(b)))
  }

  test("test_cycle_generate_argument2") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val d = Sentence("d")
    val e = Sentence("e")
    val assumptions = Set(e)

    val rule1 = Rule(Set(b), a)
    val rule2 = Rule(Set(c), b)
    val rule3 = Rule(Set(d), c)
    val rule4 = Rule(Set(b), d)
    val rule5 = Rule(Set(e), a)
    val rules = Set(rule1, rule2, rule3, rule4, rule5)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set(Set(e)))
  }

  test("test_cycle_generate_argument3") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val p = Sentence("p")
    val r = Sentence("r")
    val assumptions = Set(a, b, c)

    val rule1 = Rule(Set(a, r), p)
    val rule2 = Rule(Set(b, p), r)
    val rule3 = Rule(Set(c), p)
    val rules = Set(rule1, rule2, rule3)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(p) == Set(Set(c), Set(a, b, c)))
  }

  test("test_generate_no_arguments") {
    val a = Sentence("a")
    val b = Sentence("b")
    val assumptions = Set[Sentence]()

    val rule = Rule(Set(b), a)
    val rules = Set(rule)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(a) == Set())
  }

  test("test_complex_generate_arguments1") {
    val alpha = Sentence("alpha")
    val beta = Sentence("beta")
    val gamma = Sentence("gamma")
    val delta = Sentence("delta")
    val d = Sentence("d")
    val s1 = Sentence("s1")
    val s2 = Sentence("s2")
    val s3 = Sentence("s3")
    val s4 = Sentence("s4")
    val s5 = Sentence("s5")
    val s6 = Sentence("s6")
    val s7 = Sentence("s7")
    val assumptions = Set(alpha, beta, gamma, delta)

    val rule1 = Rule(Set(s3, s4), d)
    val rule2 = Rule(Set(s2), s3)
    val rule3 = Rule(Set(s1, beta), s2)
    val rule4 = Rule(Set(beta), s1)
    val rule5 = Rule(Set(s5, s6), d)
    val rule6 = Rule(Set(s7), s5)
    val rule7 = Rule(Set(alpha, beta, gamma), s7)
    val rule8 = Rule(Set(s2), s6)
    val rule9 = Rule(Set(s5), s7)
    val rule10 = Rule(Set(s2), s1)
    val rules = Set(rule1, rule2, rule3, rule4, rule5,
                    rule6, rule7, rule8, rule9, rule10)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(alpha) == Set(Set(alpha)))
    assert(abap.generate_arguments(beta) == Set(Set(beta)))
    assert(abap.generate_arguments(gamma) == Set(Set(gamma)))
    assert(abap.generate_arguments(delta) == Set(Set(delta)))
    assert(abap.generate_arguments(d) == Set(Set(alpha, beta, gamma)))
  }

  test("test_complex_generate_arguments2") {
    val alpha = Sentence("alpha")
    val beta = Sentence("beta")
    val gamma = Sentence("gamma")
    val delta = Sentence("delta")
    val epsilon = Sentence("epsilon")
    val d = Sentence("d")
    val s1 = Sentence("s1")
    val s2 = Sentence("s2")
    val s3 = Sentence("s3")
    val s4 = Sentence("s4")
    val s5 = Sentence("s5")
    val s6 = Sentence("s6")
    val s7 = Sentence("s7")
    val s8 = Sentence("s8")
    val s9 = Sentence("s9")
    val s10 = Sentence("s10")
    val assumptions = Set(alpha, beta, gamma, delta, epsilon)

    val rule1 = Rule(Set(alpha, beta), s1)
    val rule2 = Rule(Set(), s2)
    val rule3 = Rule(Set(s1, s2), s3)
    val rule4 = Rule(Set(alpha, s3), s4)
    val rule5 = Rule(Set(s4, beta), s5)
    val rule6 = Rule(Set(s5, gamma), s4)
    val rule7 = Rule(Set(s4, s5, s6), d)
    val rule8 = Rule(Set(s7), s6)
    val rule9 = Rule(Set(gamma, s8), s7)
    val rule10 = Rule(Set(s9), s8)
    val rule11 = Rule(Set(s2, gamma), s10)
    val rule12 = Rule(Set(s10), d)
    val rules = Set(rule1, rule2, rule3, rule4, rule5, rule6,
                    rule7, rule8, rule9, rule10, rule11, rule12)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(alpha) == Set(Set(alpha)))
    assert(abap.generate_arguments(beta) == Set(Set(beta)))
    assert(abap.generate_arguments(gamma) == Set(Set(gamma)))
    assert(abap.generate_arguments(delta) == Set(Set(delta)))
    assert(abap.generate_arguments(epsilon) == Set(Set(epsilon)))
    assert(abap.generate_arguments(d) == Set(Set(gamma)))
  }

  test("test_complex_generate_arguments3") {
    val alpha = Sentence("alpha")
    val beta = Sentence("beta")
    val a = Sentence("a")
    val b = Sentence("b")
    val s1 = Sentence("s1")
    val s2 = Sentence("s2")
    val s3 = Sentence("s3")
    val s4 = Sentence("s4")
    val s5 = Sentence("s5")
    val s6 = Sentence("s6")
    val s7 = Sentence("s7")
    val assumptions = Set(alpha, beta)

    val rule1 = Rule(Set(s1), a)
    val rule2 = Rule(Set(s2, s3), s1)
    val rule3 = Rule(Set(), s2)
    val rule4 = Rule(Set(s4), s3)
    val rule5 = Rule(Set(), s4)
    val rule6 = Rule(Set(s5, s6), b)
    val rule7 = Rule(Set(), s5)
    val rule8 = Rule(Set(beta), s6)
    val rule9 = Rule(Set(beta), s7)

    val rules = Set(rule1, rule2, rule3, rule4, rule5, rule6, rule7, rule8, rule9)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_arguments(alpha) == Set(Set(alpha)))
    assert(abap.generate_arguments(beta) == Set(Set(beta)))
    assert(abap.generate_arguments(a) == Set(Set()))
    assert(abap.generate_arguments(b) == Set(Set(beta)))
  }

  test("test_simple_generate_arguments_and_attacks1") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val assumptions = Set(a, b, c)

    val rule = Rule(Set(a, c), b.contrary)
    val rules = Set(rule)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val pref2 = Preference(c, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1, pref2)

    val abap = new ABA_Plus(assumptions, preferences, rules)

    val res = abap.generate_arguments_and_attacks_for_contraries
    val deductions = res._1
    val attacks = res._2

    assert(deductions(a) == Set(Deduction(Set(a), Set(a))))
    assert(deductions(b) == Set(Deduction(Set(b), Set(b))))
    assert(deductions(c) == Set(Deduction(Set(c), Set(c))))
    assert(deductions(b.contrary) == Set(Deduction(Set(a, c), Set(b.contrary))))
    assert(deductions.size == 4)

    assert(attacks == Set
      (Attack(Deduction(Set(b), Set(b)), Deduction(Set(a, c), Set(b.contrary)),
              AttackType.REVERSE_ATK)))
  }

  test("test_simple_generate_arguments_and_attacks2") {
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

    val res = abap.generate_arguments_and_attacks(
      Set(a.contrary, b.contrary, c.contrary))
    val deductions = res._1
    val attacks = res._2

    val ded_a = Deduction(Set(a), Set(a))
    val ded_b = Deduction(Set(b), Set(b))
    val ded_c = Deduction(Set(c), Set(c))
    val ded_contr_a = Deduction(Set(b, c), Set(a.contrary))
    val ded_contr_b = Deduction(Set(a, c), Set(b.contrary))
    val ded_contr_c = Deduction(Set(a, b), Set(c.contrary))

    assert(deductions(a) == Set(ded_a))
    assert(deductions(b) == Set(ded_b))
    assert(deductions(c) == Set(ded_c))
    assert(deductions(a.contrary) == Set(ded_contr_a))
    assert(deductions(b.contrary) == Set(ded_contr_b))
    assert(deductions(c.contrary) == Set(ded_contr_c))
    assert(deductions.size == 6)

    assert(attacks == Set
      (Attack(ded_b, ded_contr_b, AttackType.REVERSE_ATK),
       Attack(ded_contr_a, ded_a, AttackType.NORMAL_ATK),
       Attack(ded_contr_c, ded_c, AttackType.NORMAL_ATK),
       Attack(ded_contr_a, ded_contr_c, AttackType.NORMAL_ATK),
       Attack(ded_contr_a, ded_contr_b, AttackType.NORMAL_ATK),
       Attack(ded_contr_a, ded_contr_b, AttackType.REVERSE_ATK),
       Attack(ded_contr_c, ded_contr_a, AttackType.NORMAL_ATK),
       Attack(ded_contr_c, ded_contr_b, AttackType.REVERSE_ATK),
       Attack(ded_contr_c, ded_contr_b, AttackType.NORMAL_ATK)))
  }

  test("test_generate_all_deductions") {
    val a = Sentence("a")
    val b = Sentence("b")
    val c = Sentence("c")
    val e = Sentence("e")
    val f = Sentence("f")
    val g = Sentence("g")
    val assumptions = Set(a, b, e)

    val rule1 = Rule(Set(a, b), c)
    val rule2 = Rule(Set(e), f)
    val rule3 = Rule(Set(c, f), g)
    val rules = Set(rule1, rule2, rule3)

    val pref1 = Preference(a, b, PreferenceRelation.LESS_THAN)
    val pref2 = Preference(c, b, PreferenceRelation.LESS_THAN)
    val preferences = Set(pref1, pref2)

    val abap = new ABA_Plus(assumptions, Set(), rules)

    assert(abap.generate_all_deductions(Set(a,b,e)) == Set(a,b,c,e,f,g))
  }
}
