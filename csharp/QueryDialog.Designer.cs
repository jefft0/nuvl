namespace Nuvl
{
  partial class QueryDialog
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.textEdit_ = new System.Windows.Forms.TextBox();
      this.questionLabel_ = new System.Windows.Forms.Label();
      this.okButton_ = new System.Windows.Forms.Button();
      this.cancelButton_ = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // textEdit_
      // 
      this.textEdit_.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.textEdit_.Location = new System.Drawing.Point(12, 51);
      this.textEdit_.Name = "textEdit_";
      this.textEdit_.Size = new System.Drawing.Size(296, 20);
      this.textEdit_.TabIndex = 0;
      this.textEdit_.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textEdit__KeyPress);
      // 
      // questionLabel_
      // 
      this.questionLabel_.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.questionLabel_.Location = new System.Drawing.Point(12, 9);
      this.questionLabel_.Name = "questionLabel_";
      this.questionLabel_.Size = new System.Drawing.Size(296, 39);
      this.questionLabel_.TabIndex = 1;
      // 
      // okButton_
      // 
      this.okButton_.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.okButton_.Location = new System.Drawing.Point(49, 87);
      this.okButton_.Name = "okButton_";
      this.okButton_.Size = new System.Drawing.Size(75, 23);
      this.okButton_.TabIndex = 2;
      this.okButton_.Text = "OK";
      this.okButton_.UseVisualStyleBackColor = true;
      this.okButton_.Click += new System.EventHandler(this.okButton__Click);
      // 
      // cancelButton_
      // 
      this.cancelButton_.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.cancelButton_.Location = new System.Drawing.Point(157, 87);
      this.cancelButton_.Name = "cancelButton_";
      this.cancelButton_.Size = new System.Drawing.Size(75, 23);
      this.cancelButton_.TabIndex = 3;
      this.cancelButton_.Text = "Cancel";
      this.cancelButton_.UseVisualStyleBackColor = true;
      this.cancelButton_.Click += new System.EventHandler(this.cancelButton__Click);
      // 
      // QueryDialog
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.BackColor = System.Drawing.Color.Gainsboro;
      this.ClientSize = new System.Drawing.Size(320, 133);
      this.Controls.Add(this.cancelButton_);
      this.Controls.Add(this.okButton_);
      this.Controls.Add(this.questionLabel_);
      this.Controls.Add(this.textEdit_);
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "QueryDialog";
      this.Text = "Question";
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.TextBox textEdit_;
    private System.Windows.Forms.Label questionLabel_;
    private System.Windows.Forms.Button okButton_;
    private System.Windows.Forms.Button cancelButton_;
  }
}