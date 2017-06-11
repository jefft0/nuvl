/*
Copyright (C) 2017 Jeff Thompson
From https://github.com/zb95/2016-ABAPlus/blob/master/aspartix_interface.py
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
import java.io.BufferedReader
import java.io.BufferedWriter
import java.io.FileWriter
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import scala.util.matching.Regex

/**
 * ASPARTIX_Interface provides an interface to an ASP solver to calculate
 * extensions of the ABA+ framework in aba_plus. Also generate a file from
 * aba_plus which can be fed into an ASP solver.
 * @param aba_plus The ABA+ framework.
 * @param input_filename (optional) The name of the file to generate. It is
 * written to the urrent working directory. The file name is remembered and used
 * in methods like calculate_admissible_extensions. If omitted, use a default
 * file name.
 */
final class ASPARTIX_Interface
  (val aba_plus: ABA_Plus, val input_filename: String = "temp-asp-input99.lp") {
  import ASPARTIX_Interface._

  // Note: The Python implementation uses generate_input_file_for_clingo as a
  // separate method, but we call it here in the constructor so that we
  // remember the input_file_path which is derived from aba_plus.
  val arguments = generate_input_file_for_clingo()

  /**
   * Generate a file from aba_plus which can be fed into an ASP solver, and
   * save it to input_filename in the current working directory.
   * @return The array of arguments for the constructor to remember, where each
   * argument is its set of assumptions.
   */
  private def generate_input_file_for_clingo() = {
    val res = aba_plus.generate_arguments_and_attacks_for_contraries
    val deductions = res._3
    val generatedAttacks = res._2

    // Convert the set of arguments to an array were each index is used to
    // represent the argument in the input file. (And each argument is
    // determined by its set of assumptions in a deduction.)
    val arguments = deductions.map(_.premise).toArray

    val attacks = generatedAttacks.map(atk =>
      (atk.attacker.premise, atk.attackee.premise))

    var f = new BufferedWriter(new FileWriter(input_filename))

    for (idx <- 0 until arguments.size)
      f.write("arg(" + idx + ").\n")

    for (atk <- attacks) {
      val idx_attacker = arguments.indexOf(atk._1)
      val idx_attackee = arguments.indexOf(atk._2)
      f.write("att(" + idx_attacker + ", " + idx_attackee + ").\n")
    }

    f.close

    arguments
  }

  /**
   * Calculate the admissible extensions of the ABA+ framework given to the
   * constructor.
   * @return The set of extensions where each extension is the set of assumptions.
   */
  def calculate_admissible_extensions = calculate_extensions(
    CLINGO_COMMAND_NAME, CLINGO_COMMAND_ARGS, ADMISSIBLE_DL, CLINGO_ANSWER, CLINGO_REGEX)

  /**
   * Calculate the stable extensions of the ABA+ framework given to the
   * constructor.
   * @return The set of extensions where each extension is the set of assumptions.
   */
  def calculate_stable_extensions = calculate_extensions(
    CLINGO_COMMAND_NAME, CLINGO_COMMAND_ARGS, STABLE_DL, CLINGO_ANSWER, CLINGO_REGEX)

  // TODO: calculate_ideal_extensions

  /**
   * Calculate the complete extensions of the ABA+ framework given to the
   * constructor.
   * @return The set of extensions where each extension is the set of assumptions.
   */
  def calculate_complete_extensions = calculate_extensions(
    CLINGO_COMMAND_NAME, CLINGO_COMMAND_ARGS, COMPLETE_DL, CLINGO_ANSWER, CLINGO_REGEX)

  /**
   * Calculate the preferred extensions of the ABA+ framework given to the
   * constructor.
   * @return The set of extensions where each extension is the set of assumptions.
   */
  def calculate_preferred_extensions = calculate_extensions(
    CLINGO_COMMAND_NAME, CLINGO_COMMAND_ARGS, PREFERRED_DL, CLINGO_ANSWER, CLINGO_REGEX)

  /**
   * Calculate the grounded extensions of the ABA+ framework given to the
   * constructor.
   * @return The set of extensions where each extension is the set of assumptions.
   */
  def calculate_grounded_extensions = calculate_extensions(
    CLINGO_COMMAND_NAME, CLINGO_COMMAND_ARGS, GROUNDED_DL, CLINGO_ANSWER, CLINGO_REGEX)

  /**
   * Execute the external solver to calculate the extensions of the ABA+
   * framework given to the constructor. This runs the command
   * "commandName input_filename - commandArgs" where input_filename is the
   * name of the file generated in the constructor. The second argument is "-"
   * so that encoding_dl is piped to stdin of the command. Scan the command
   * output for answer_header to separate each amswer (extension). Use regex to
   * find the set of argument numbers in each answer which is used to get the
   * set of assumptions from the arguments array created in the constructor,
   * then the extension is the union of the assumptions in the answer.
   * @param commandName The command to run, which should be on the system's
   * search PATH, e.g. "clingo".
   * @param commandArgs The extra arguments to add to the command line after the
   * input_filename and "-", e.g. "0".
   * @param encoding_dl The semantics to pipe to stdin of the command as the
   * second argument, e.g. ASPARTIX_Interface.ADMISSIBLE_DL .
   * @param answer_header The answer header string that the desired solver
   * outputs, e.g. "Answer:".
   * @param regex The regular expression which matches the integer in the
   * answer of the solver output which is used to find the argument number in
   * arguments, e.g. """in\((\d+)\)""".r  .
   * @return The set of extensions where each extension is the set of assumptions.
   */
  private def calculate_extensions
    (commandName: String, commandArgs: String, encoding_dl: String,
     answer_header: String, regex: Regex) = {
    val args = commandName + " " + input_filename + " - " + commandArgs

    val process = Runtime.getRuntime().exec(args)

    // Send the encoding file to the process.
    val writer = new BufferedWriter(new OutputStreamWriter(process.getOutputStream()));
    writer.write(encoding_dl)
    writer.flush();
    writer.close();

    // Read the output from the process.
    val reader = new BufferedReader(new InputStreamReader(process.getInputStream()))
    val stringBuilder = new StringBuilder()
    var line = ""
    while (line != null) {
      line = reader.readLine()
      if (line != null) {
        stringBuilder.append(line)
        stringBuilder.append("\n")
      }
    }
    process.waitFor()
    val res = stringBuilder.toString()

    if (!res.contains(answer_header))
      Set[Set[Sentence]]()
    else {
      var results = res.split(answer_header)

      var extension_sets = mutable.Set[Set[Sentence]]()
      for (i <- 1 until results.size) {
        var answer = results(i)
        var matches = regex.findAllMatchIn(answer)
        var extension = mutable.Set[Sentence]()
        for (m <- matches) {
          var arg = arguments(m.group(1).toInt)
          extension ++= arg
        }
        extension_sets += extension.toSet
      }

      extension_sets.toSet
    }
  }
}

object ASPARTIX_Interface {
  val CLINGO_COMMAND_NAME = "clingo"
  val CLINGO_COMMAND_ARGS = "0"

  val CLINGO_ANSWER = "Answer:"
  val DLV_ANSWER = "Best model:"

  val CLINGO_REGEX = """in\((\d+)\)""".r
  val DLV_IDEAL_REGEX = """ideal\((\d+)\)""".r

  // https://github.com/zb95/2016-ABAPlus/blob/9dc69ea5e8cb5e88bb9995a6540ede45812de5e6/adm.dl
  val ADMISSIBLE_DL = """
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Encoding for admissible extensions
%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% an argument x defeats an argument y if x attacks y
defeat(X,Y) :- att(X,Y),
	       not vaf.

%% Guess a set S \subseteq A
in(X) :- not out(X), arg(X).
out(X) :- not in(X), arg(X).

%% S has to be conflict-free
:- in(X), in(Y), defeat(X,Y).

%% The argument x is defeated by the set S
defeated(X) :- in(Y), defeat(Y,X).

%% The argument x is not defended by S
not_defended(X) :- defeat(Y,X), not defeated(Y).

%% All arguments x \in S need to be defended by S
:- in(X), not_defended(X).
"""

  // https://github.com/zb95/2016-ABAPlus/blob/9dc69ea5e8cb5e88bb9995a6540ede45812de5e6/stable.dl
  val STABLE_DL = """
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Encoding for stable extensions
%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% an argument x defeats an argument y if x attacks y
defeat(X,Y) :- att(X,Y),
               not vaf.

%% Guess a set S \subseteq A
in(X) :- not out(X), arg(X).
out(X) :- not in(X), arg(X).

%% S has to be conflict-free
:- in(X), in(Y), defeat(X,Y).

%% The argument x is defeated by the set S
defeated(X) :- in(Y), defeat(Y,X).

%% S defeats all arguments which do not belong to S
:- out(X), not defeated(X).
"""

  // https://github.com/zb95/2016-ABAPlus/blob/9dc69ea5e8cb5e88bb9995a6540ede45812de5e6/ideal.dl
  val IDEAL_DL = """
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Encoding for ideal extension
%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% The program ADM:
%
%% guess set S \subseteq A
% in(X) :- not out(X), arg(X).
% out(X) :- not in(X), arg(X).
%% cf
% :- in(X), in(Y), att(X,Y).
%% argument x is defeated by S
% def(X) :- in(Y), att(Y,X).
%% admissible
% :- in(X), att(Y,X), not def(Y).
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%% ADM_{in}^bc, where
%  d_inIn stands for d_in^in
%  d_outIn stands for d_out^in
%  d_defeatedIn stands for d_def^in
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
c :- not i.
i :- not c.

%% guess
d_inIn(X,Y) :- c, not d_outIn(X,Y), arg(X), arg(Y).
d_outIn(X,Y) :- c, not d_inIn(X,Y), arg(X), arg(Y).

%% cf
:- c, d_inIn(X,Z), d_inIn(Y,Z), att(X,Y).

%% defeated argument x
d_defeatedIn(X,Z) :- c, d_inIn(Y,Z), att(Y,X).

%% adm
:- c, d_inIn(X,Z), att(Y,X), not d_defeatedIn(Y,Z).

%% brave consequence
:~ not d_inIn(X,X), arg(X).
:~ i.

%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%% auxiliary rules for ideal semanitcs
%% in_minus\1 stands for X_F^-
%% in_plus\1 stands for X_F^+
%% q\2 stands for R^*
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
in(X) :- d_inIn(X,X).
in_minus(X) :- arg(X), not in(X).
not_in_plus(X) :- in(Y), att(X,Y).
not_in_plus(X) :- in(Y), att(Y,X).
in_plus(X) :- in(X), not not_in_plus(X).
q(X,Y) :- att(X,Y), in_plus(X), in_minus(Y).
q(X,Y) :- att(X,Y), in_minus(X), in_plus(Y).

%% defining an order over in+\1
lt(X,Y) :- in_plus(X),in_plus(Y), X<Y.
nsucc(X,Z) :- lt(X,Y), lt(Y,Z).
succ(X,Y) :- lt(X,Y), not nsucc(X,Y).
ninf(X) :- lt(Y,X).
nsup(X) :- lt(X,Y).
inf(X) :- not ninf(X), in_plus(X).
sup(X) :- not nsup(X), in_plus(X).

%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%% computing arguments that are in ideal extension using nideal\2
%% first argument is the iteration step.
%% 1. iteration: all arguments in X_F^+ which are attacked
%%    		 by an unattacked argument are collected.
%% next iterations: all arg. from previous steps and arg. that
%% 		    are attacked by an arg. that is unattacked
%%		    by X_F^+
%% ideal: arg. that are not excluded from X_F^+ in the final iteration
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
attacked(X) :- q(Y,X).
nideal(I,Y) :- inf(I), q(Z,Y), in_plus(Y), not attacked(Z).
nideal(I,Y) :- succ(J,I), nideal(J,Y).
nideal(I,Y) :- succ(J,I), q(Z,Y), in_plus(Y), not attacked_upto(J,Z).
attacked_upto(J,Z) :- q(Y,Z), in_plus(Y), not nideal(J,Y), in_plus(J).
ideal(X) :- in_plus(X), sup(I), not nideal(I,X).

%ideal(X)?
"""

  // https://github.com/zb95/2016-ABAPlus/blob/9dc69ea5e8cb5e88bb9995a6540ede45812de5e6/comp.dl
  val COMPLETE_DL = """
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Encoding for complete extensions
%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% an argument x defeats an argument y if x attacks y
defeat(X,Y) :- att(X,Y),
               not vaf.

%% Guess a set S \subseteq A
in(X) :- not out(X), arg(X).
out(X) :- not in(X), arg(X).

%% S has to be conflict-free
:- in(X), in(Y), defeat(X,Y).

%% The argument x is defeated by the set S
defeated(X) :- in(Y), defeat(Y,X).

%% The argument x is not defended by S
not_defended(X) :- defeat(Y,X), not defeated(Y).

%% admissible
:- in(X), not_defended(X).

%% Every argument which is defended by S belongs to S
:- out(X), not not_defended(X).
"""

  // https://github.com/zb95/2016-ABAPlus/blob/9dc69ea5e8cb5e88bb9995a6540ede45812de5e6/prefex_gringo.lp
  val PREFERRED_DL = """
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Encoding for preferred extensions
%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% an argument x defeats an argument y if x attacks y
defeat(X,Y) :- att(X,Y),
               not vaf.

%% Guess a set S \subseteq A
in(X) :- not out(X), arg(X).
out(X) :- not in(X), arg(X).

%% S has to be conflict-free
:- in(X), in(Y), defeat(X,Y).

%% The argument x is defeated by the set S
defeated(X) :- in(Y), defeat(Y,X).

%% The argument x is not defended by S
not_defended(X) :- defeat(Y,X), not defeated(Y).

%% All arguments x \in S need to be defended by S (admissibility)
:- in(X), not_defended(X).

%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% For the remaining part we need to put an order on the domain.
% Therefore, we define a successor-relation with infinum and supremum
% as follows
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

lt(X,Y) :- arg(X),arg(Y), X<Y, not input_error.
nsucc(X,Z) :- lt(X,Y), lt(Y,Z).
succ(X,Y) :- lt(X,Y), not nsucc(X,Y).
ninf(X) :- lt(Y,X).
nsup(X) :- lt(X,Y).
inf(X) :- not ninf(X), arg(X).
sup(X) :- not nsup(X), arg(X).


%% Guess S' \supseteq S
inN(X) :- in(X).
inN(X) | outN(X) :- out(X).

%% If S' = S then spoil.
%% Use the sucessor function and check starting from supremum whether
%% elements in S' is also in S. If this is not the case we "stop"
%% If we reach the supremum we spoil up.

% eq indicates whether a guess for S' is equal to the guess for S

eq_upto(Y) :- inf(Y), in(Y), inN(Y).
eq_upto(Y) :- inf(Y), out(Y), outN(Y).

eq_upto(Y) :- succ(Z,Y), in(Y), inN(Y), eq_upto(Z).
eq_upto(Y) :- succ(Z,Y), out(Y), outN(Y), eq_upto(Z).

eq :- sup(Y), eq_upto(Y).


%% get those X \notin S' which are not defeated by S'
%% using successor again...

undefeated_upto(X,Y) :- inf(Y), outN(X), outN(Y).
undefeated_upto(X,Y) :- inf(Y), outN(X),  not defeat(Y,X).

undefeated_upto(X,Y) :- succ(Z,Y), undefeated_upto(X,Z), outN(Y).
undefeated_upto(X,Y) :- succ(Z,Y), undefeated_upto(X,Z), not defeat(Y,X).

undefeated(X) :- sup(Y), undefeated_upto(X,Y).

%% spoil if the AF is empty
not_empty :- arg(X).
spoil :- not not_empty.

%% spoil if S' equals S for all preferred extensions
spoil :- eq.

%% S' has to be conflict-free - otherwise spoil
spoil :- inN(X), inN(Y), defeat(X,Y).

%% S' has to be admissible - otherwise spoil
spoil :- inN(X), outN(Y), defeat(Y,X), undefeated(Y).

inN(X) :- spoil, arg(X).
outN(X) :- spoil, arg(X).

%% do the final spoil-thing ...
:- not spoil.

%in(X)?
#show in/1.
"""

  // https://github.com/zb95/2016-ABAPlus/blob/9dc69ea5e8cb5e88bb9995a6540ede45812de5e6/ground.dl
  val GROUNDED_DL = """
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Encoding for grounded extensions
%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% an argument x defeats an argument y if x attacks y
defeat(X,Y) :- att(X,Y).

%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% For the remaining part we need to put an order on the domain.
% Therefore, we define a successor-relation with infinum and supremum
% as follows
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

lt(X,Y) :- arg(X),arg(Y), X<Y, not input_error.
nsucc(X,Z) :- lt(X,Y), lt(Y,Z).
succ(X,Y) :- lt(X,Y), not nsucc(X,Y).
ninf(X) :- lt(Y,X).
nsup(X) :- lt(X,Y).
inf(X) :- not ninf(X), arg(X).
sup(X) :- not nsup(X), arg(X).

%% we now fill up the predicate in(.) with arguments which are defended

defended_upto(X,Y) :- inf(Y), arg(X), not defeat(Y,X).
defended_upto(X,Y) :- inf(Y), in(Z), defeat(Z,Y), defeat(Y,X).
defended_upto(X,Y) :- succ(Z,Y), defended_upto(X,Z), not defeat(Y,X).
defended_upto(X,Y) :- succ(Z,Y), defended_upto(X,Z), in(V), defeat(V,Y), defeat(Y,X).

defended(X) :- sup(Y), defended_upto(X,Y).
in(X) :- defended(X).
"""
}
