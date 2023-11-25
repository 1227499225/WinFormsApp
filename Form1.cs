using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void customTextBoxControl1_TextChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("TextChanged1：" + customTextBoxControl1.Text) ;
        }
        private void customTextBoxControl1_CustomTextChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("TextChanged1111：" + customTextBoxControl1.Text) ;
        }
    }
}
