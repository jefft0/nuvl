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

import scala.collection.mutable

/**
 * ABA_Plus represents an ABA+ framework and its components (assumptions, rules
 * and preferences). Note that the set of preferences stored in this object is
 * the transitive closure of preferencesIn. Also note that this class doesn't
 * define member-wise equality and can't be used as a map key.'
 * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L32
 */
final class ABA_Plus
  (val assumptions: Set[Sentence], preferencesIn: Set[Preference], val rules: Set[Rule]) {
  import ABA_Plus._

  if (!is_flat())
    throw NonFlatException("The framework is not flat!")

  // Note that this throws CyclicPreferenceException if needed.
  val preferences: Set[Preference] =
    if (preferencesIn.isEmpty)
      // In this simple case, avoid calling calc_transitive_closure.
      preferencesIn
    else
      preferencesIn union ABA_Plus.calc_transitive_closure(assumptions, preferencesIn)

  if (!preferences_only_between_assumptions())
    throw InvalidPreferenceException("Non-assumption in preference detected!")

  // TODO: check_or_auto_WCP

  /**
   * Check if the ABA+ framework is flat.
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L71
   * @return True if framework is flat, false otherwise.
   */
  def is_flat() = !rules.exists(assumptions contains _.consequent)

  /**
   * Check if preference relations are only between assumptions.
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L82
   * @return True if only between assumptions, false otherwise
   */
  def preferences_only_between_assumptions() =
    !preferences.exists(pref => !(assumptions contains pref.assump1) ||
                                !(assumptions contains pref.assump2))

  /**
   * Get the set of all rules deriving the sentence.
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L143
   * @param sentence The Sentence which is a rule consequent.
   * @return The set of rules.
   */
  def deriving_rules(sentence: Sentence) = rules.filter(_.consequent == sentence)

  /**
   * Get the strongest relation between two assumptions, assump1 and assump2.
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L154
   * @param assump1 The first assumption.
   * @param assump2 The second assumption.
   * @return The strongest relation, or PreferenceRelation.NO_RELATION if no
   * match is found in preferences.
   */
  def get_relation(assump1: Sentence, assump2: Sentence) =
    preferences.foldLeft(PreferenceRelation.NO_RELATION) {
      (strongest_relation_found, pref) =>
        if (pref.assump1 == assump1 && pref.assump2 == assump2 &&
            pref.relation < strongest_relation_found)
          pref.relation 
        else
          strongest_relation_found
    }

  /**
   * Check if the relation assump2 < assump1 exists.
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L165
   * @param assump1 The first assumption.
   * @param assump2 The second assumption.
   * @return True if the relation assump2 < assump1 exists, false otherwise.
   */
  def is_preferred(assump1: Sentence, assump2: Sentence) =
    get_relation(assump2, assump1) == PreferenceRelation.LESS_THAN

  /**
   * Check if to_deduce can be deduced from deduce_from.
   * https://github.com/zb95/2016-ABAPlus/blob/a8bfcfcfa3092f0aca7aa08e5f1b06545730f270/aba_plus_.py#L172
   * @param to_deduce A Sentence to deduce.
   * @param deduce_from A set of Sentences to deduce from.
   * @return True, if to_deduce can be deduced from deduce_from.
   */
  def deduction_exists(to_deduce: Sentence, deduce_from: Set[Sentence]) = {
    val rules_applied = mutable.Set[Rule]()
    val deduced = mutable.Set[Sentence]()
    deduced ++= deduce_from
    var new_rule_used = true
    var found_deduction = false
    while (!found_deduction && new_rule_used) {
      new_rule_used = false
      // Use exists to quit when found_deduction is true.
      found_deduction = rules.exists(rule => {
        if (!(rules_applied contains rule)) {
          if (rule.antecedent subsetOf deduced) {
            new_rule_used = true
            if (rule.consequent == to_deduce)
              found_deduction = true
            else
              deduced += rule.consequent
            rules_applied += rule
          }
        }

        found_deduction
      })
    }

    found_deduction
  }

  /**
   * Get all Sentences that can be derived from deduce_from.
   * // https://github.com/zb95/2016-ABAPlus/blob/a8bfcfcfa3092f0aca7aa08e5f1b06545730f270/aba_plus_.py#L195
   * @param deduce_from A set of Sentences.
   * @return A set of all Sentences that can be derived from deduce_from.
   */
  def generate_all_deductions(deduce_from: Set[Sentence]) = {
    val rules_applied = mutable.Set[Rule]()
    val deduced = mutable.Set[Sentence]()
    deduced ++= deduce_from
    var new_rule_used = true
    while (new_rule_used) {
      new_rule_used = false
      for (rule <- rules) {
        if (!(rules_applied contains rule)) {
          if (rule.antecedent subsetOf deduced) {
            new_rule_used = true
            deduced += rule.consequent
            rules_applied += rule
          }
        }
      }
    }

    deduced.toSet
  }

  // TODO: check_WCP
  // TODO: check_and_partially_satisfy_WCP
  // TODO: _WCP_fulfilled
  // TODO: get_minimally_preferred

  /**
   * Find sets of assumptions which deduce the generate_for sentence.
   * TODO: rename to avoid confusion between supporting sets and 'arguments' in
   * abstract argumentation.
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L310
   * @param generate_for A Sentence which is deduced.
   * @return An immutable set of sets of assumptions, where each set contains
   * assumptions deducing generate_for.
   */
  def generate_arguments(generate_for: Sentence) =
    _generate_arguments(generate_for, mutable.Set()).toSet

  // https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L317
  private def _generate_arguments
    (generate_for: Sentence, rules_seen: mutable.Set[Rule]): mutable.Set[Set[Sentence]] = {
    if (assumptions contains generate_for)
      mutable.Set(Set(generate_for))
    else {
      val der_rules = deriving_rules(generate_for)
      val results = mutable.Set[Set[Sentence]]()

      for (rule <- der_rules) {
        if (!(rules_seen contains rule)) {
          val supporting_assumptions = mutable.Set[mutable.Set[Set[Sentence]]]()
          var args_lacking = false
          if (rule.antecedent.isEmpty)
            supporting_assumptions += mutable.Set(Set())
          val _rules_seen = rules_seen.clone()
          _rules_seen += rule
          for (ant <- rule.antecedent) {
            if (args_lacking != true) {
              val args = _generate_arguments(ant, _rules_seen)
              if (args.isEmpty)
                args_lacking = true
                // We can't break the for loop, but we check args_lacking above.'
              else
                supporting_assumptions += args
            }
          }

          if (!args_lacking)
            results ++= set_combinations(supporting_assumptions)
        }
      }

      results
    }
  }

  /**
   * Generate arguments supporting generate_for and all attacks between the
   * arguments.
   * @param generate_for A set of Sentences.
   * @return The tuple (deductions, attacks, all_deductions) where
   * deductions is a map that maps sentences to sets of Deductions that deduce them,
   * attacks is A set of all attacks generated, and
   * all_deductions is a set of all Deductions generated.
   */
  def generate_arguments_and_attacks(generate_for: Set[Sentence]) = {
    val deductions = mutable.Map[Sentence, Set[Deduction]]()
    val attacks = mutable.Set[Attack]()
    // This maps attackees to attackers in normal attacks.
    val atk_map = mutable.Map[Sentence, mutable.Set[Set[Sentence]]]()
    // This maps attackees to attackers in reverse attacks.
    val reverse_atk_map = mutable.Map[Set[Sentence], mutable.Set[Sentence]]()

    // Generate trivial deductions for all assumptions.
    for (assumption <- assumptions)
      deductions(assumption) = Set(Deduction(Set(assumption), Set(assumption)))

    // Generate supporting assumptions.
    for (sentence <- generate_for) {
      val args = generate_arguments(sentence)
      if (!args.isEmpty) {
        deductions(sentence) = Set()

        for (arg <- args) {
          val arg_deduction = Deduction(arg, Set(sentence))
          deductions(sentence) += arg_deduction

          if (sentence.is_contrary && (assumptions contains sentence.contrary())) {
            val trivial_arg = Deduction(
              Set(sentence.contrary()), Set(sentence.contrary()))

            if (attack_successful(arg, sentence.contrary())) {
              attacks += Attack(arg_deduction, trivial_arg, AttackType.NORMAL_ATK)

              val f_arg = arg
              if (!(atk_map contains sentence.contrary()))
                atk_map(sentence.contrary()) = mutable.Set()
              atk_map(sentence.contrary()) += f_arg
            }
            else {
              attacks += Attack(trivial_arg, arg_deduction, AttackType.REVERSE_ATK)

              val f_arg = arg
              if (!(reverse_atk_map contains f_arg))
                reverse_atk_map(f_arg) = mutable.Set()
              reverse_atk_map(f_arg) += sentence.contrary()
            }
          }
        }
      }
    }

    val all_deductions = mutable.Set[Deduction]()
    for (x <- deductions.values)
      all_deductions ++= x

    for ((n_attackee, n_attacker_sets) <- atk_map) {
      val attackees = all_deductions.filter(_.premise contains n_attackee)
      for (n_attacker <- n_attacker_sets) {
        val attackers = all_deductions.filter(n_attacker subsetOf _.premise)
        for (attackee <- attackees) {
          for (attacker <- attackers)
            attacks += Attack(attacker, attackee, AttackType.NORMAL_ATK)
        }
      }
    }

    for ((r_attackee, r_attacker_sets) <- reverse_atk_map) {
      val attackees = all_deductions.filter(r_attackee subsetOf _.premise)
      for (r_attacker <- r_attacker_sets) {
        val attackers = all_deductions.filter(_.premise contains r_attacker)
        for (attackee <- attackees) {
          for (attacker <- attackers)
            attacks += Attack(attacker, attackee, AttackType.REVERSE_ATK)
        }
      }
    }

    (deductions.toMap, attacks.toSet, all_deductions.toSet)
  }

  def generate_arguments_and_attacks_for_contraries =
    generate_arguments_and_attacks(assumptions.map(asm => asm.contrary()))

  /**
   * Check if attacker attacks attackee successfully,
   * @param attacker A set of Sentences.
   * @param attackee A Sentence.
   * @return True if attacker attacks attackee successfully, false otherwise.
   */
  def attack_successful(attacker: Set[Sentence], attackee: Sentence) =
    !attacker.exists(is_preferred(attackee, _))

  // TODO: attacking_sentences_less_than_attackee (appaently unused)
}

case class CyclicPreferenceException(message: String = "", cause: Throwable = null)
  extends Exception(message, cause)

case class NonFlatException(message: String = "", cause: Throwable = null)
  extends Exception(message, cause)

case class InvalidPreferenceException(message: String = "", cause: Throwable = null)
  extends Exception(message, cause)

case class WCPViolationException(message: String = "", cause: Throwable = null)
  extends Exception(message, cause)

object ABA_Plus {
  /**
   * Calculate the transitive closure of preference relations. These should be
   * added to the given set of preferences.
   * Note that the Python implementation directly modifies the ABA_Plus set of
   * preferences, but this is a static method to return a set of Preferences to
   * be added. The reason is that the ABA_Plus set of preferences is immutable,
   * so we form the union in the ABA_Plus constructor. Also, instead of
   * returning false for a detected cycle, we throw the
   * CyclicPreferenceException here.
   * https://github.com/zb95/2016-ABAPlus/blob/a8bfcfcfa3092f0aca7aa08e5f1b06545730f270/aba_plus_.py#L93
   * @param assumptions The set of Assumptions.
   * @param preferences The set of Preferences.
   * @return The set of extra Preferences to create the transitive closure.
   * @throws CyclicPreferenceException if a cycle in preference relations is
   * detected.
   */
  private def calc_transitive_closure
    (assumptions: Set[Sentence], preferences: Set[Preference]) = {
    val extraPreferences = mutable.Set[Preference]()

    val assump_list = assumptions.toArray
    val m = assump_list.size
    val relation_matrix = full(m, m, PreferenceRelation.NO_RELATION)
    fill_diagonal(relation_matrix, PreferenceRelation.LESS_EQUAL)
    for (pref <- preferences) {
      val idx1 = assump_list.indexOf(pref.assump1)
      val idx2 = assump_list.indexOf(pref.assump2)
      relation_matrix(idx1)(idx2) = pref.relation
    }

    val closed_matrix = _transitive_closure(relation_matrix)

    for {i <- 0 until m
         j <- 0 until m} {
      var relation = closed_matrix(i)(j)
      // Cycle detected.
      if (i == j && relation == PreferenceRelation.LESS_THAN)
        throw CyclicPreferenceException("Cycle in preferences detected!")
      if (i != j && relation != PreferenceRelation.NO_RELATION) {
        val assump1 = assump_list(i)
        val assump2 = assump_list(j)
        extraPreferences += Preference(assump1, assump2, relation)
      }
    }

    extraPreferences
  }

  /**
   * Calculate the transitive closure given the relation matrix.
   * https://github.com/zb95/2016-ABAPlus/blob/a8bfcfcfa3092f0aca7aa08e5f1b06545730f270/aba_plus_.py#L123
   * @param relation_matrix: The relation matrix, with 
   * PreferenceRelation.LESS_THAN and PreferenceRelation.LESS_EQUAL.
   * @return A new relation matrix of the transitive closure.
   */
  private def _transitive_closure
    (relation_matrix: Array[Array[PreferenceRelation.Value]]) = {
    val n = relation_matrix.size
    val d = copy(relation_matrix)

    for {k <- 0 until n
         i <- 0 until n
         j <- 0 until n} {
      var alt_rel = PreferenceRelation.NO_RELATION
      if (!(d(i)(k) == PreferenceRelation.NO_RELATION ||
            d(k)(j) == PreferenceRelation.NO_RELATION))
        alt_rel = if (d(i)(k) < d(k)(j)) d(i)(k) else d(k)(j)

      if (alt_rel < d(i)(j))
        d(i)(j) = alt_rel
    }

    d
  }

  /**
   * Compute all combinations of sets of sets. For example:
   * set_combinations({{{b}},{{e}, {f}}}) returns {{b,e},{b,f}} .
   * https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L214
   * @param iterable The iterable whose members are a sets of sets.
   * @return the set of combinations, where a combination is a set.
   */
  def set_combinations[T](iterable: Iterable[mutable.Set[Set[T]]]) =
    _set_combinations(iterable.iterator)

  // https://github.com/zb95/2016-ABAPlus/blob/f619e7a982d3b19a76ed64bb5fe5dd11b22dad72/aba_plus_.py#L222
  private def _set_combinations[T](iter: Iterator[mutable.Set[Set[T]]]): mutable.Set[Set[T]] =
    if (iter.hasNext) {
      val current_set = iter.next
      val sets_to_combine_with = _set_combinations(iter)
      val resulting_combinations = mutable.Set[Set[T]]()
      for (c <- current_set) {
        if (sets_to_combine_with.isEmpty)
          resulting_combinations += c
        for (s <- sets_to_combine_with)
         resulting_combinations += c.union(s)
      }

      resulting_combinations
    }
    else
      mutable.Set()

  /**
   * Imitate numpy.full to create a matrix with an initial value.
   * @param nRows The number of rows.
   * @param nColumns The number of columns.
   * @param initialValue The initial value.
   * @return A new matrix (2D array).
   */
  def full[T : Manifest](nRows: Int, nColumns: Int, initialValue: T) = {
    val matrix = Array.ofDim[T](nRows, nColumns)
    for {row <- 0 until nRows
         col <- 0 until nColumns}
      matrix(row)(col) = initialValue
      
    matrix
  }

  /**
   * Imitate numpy.copy to return a copy of the matrix.
   * @param matrix The matrix.
   * @return A new matrix (2D array).
   */
  def copy[T](matrix: Array[Array[T]]) = matrix.map(_.clone)

  /**
   * Imitate numpy.fill_diagonal to modify the matrix in place to set the
   * diagonal members to the given value.
   * @param matrix The matrix.
   * @param value The value for the diagonal.
   */
  def fill_diagonal[T](matrix: Array[Array[T]], value: T) = {
    for (i <- 0 until matrix.size)
      matrix(i)(i) = value
  }
}
