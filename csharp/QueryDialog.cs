using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nuvl
{
  /// <summary>
  /// A QueryDialog shows a question and an edit box for the user the enter the response.
  /// </summary>
  public partial class QueryDialog : Form
  {
    /// <summary>
    /// Create a dialog box to show a question and get a text response.
    /// You must call ShowDialog. If it returns DialogResult.OK, then call
    /// getText() to get the result.
    /// </summary>
    /// <param name="initialText">The initial result text.</param>
    /// <param name="question">The question text to show.</param>
    /// <param name="isPassword">If true, hide the response text while typing.</param>
    public QueryDialog(string initialText, string question, bool isPassword)
    {
      InitializeComponent();

      textEdit_.Text = initialText;
      questionLabel_.Text = question;
      textEdit_.PasswordChar = '*';
    }

    /// <summary>
    /// Create a dialog box to show a question and get a text response.
    /// You must call ShowDialog. If it returns DialogResult.OK, then call
    /// getText() to get the result.
    /// </summary>
    /// <param name="initialText">The initial result text.</param>
    /// <param name="question">The question text to show.</param>
    public QueryDialog(string initialText, string question)
      : this(initialText, question, false)
    {
    }

    /// <summary>
    /// If ShowDialog returns DialogResult.OK, then call this to get the result.
    /// </summary>
    /// <returns>The response text.</returns>
    public string
    getText() { return textEdit_.Text; }

    private void textEdit__KeyPress(object sender, KeyPressEventArgs e)
    {
      if (e.KeyChar == (char)13)
        okButton_.PerformClick();
      else if (e.KeyChar == (char)27)
        cancelButton_.PerformClick();
    }

    private void okButton__Click(object sender, EventArgs e)
    {
      DialogResult = DialogResult.OK;
    }

    private void cancelButton__Click(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
    }
  }
}
