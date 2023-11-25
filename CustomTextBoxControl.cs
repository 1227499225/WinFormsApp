using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp2
{
    public partial class CustomTextBoxControl : Control
    {
        #region 私有变量
        /// <summary>
        /// 最后一次绘制的文本
        /// </summary>
        private string _lastDrawText = string.Empty;
        /// <summary>
        /// 光标位置
        /// </summary>
        private int _cursorPosition = 0;
        /// <summary>
        /// 光标是否可见
        /// </summary>
        private bool _isCursorVisible = true;
        /// <summary>
        /// 光标闪烁计时器
        /// </summary>
        private Timer _cursorBlinkTimer;
        /// <summary>
        /// 窗口句柄 base.Handle
        /// </summary>
        private IntPtr _himc;
        /// <summary>
        /// 鼠标左键是否按下
        /// </summary>
        private bool _isMouseLeftDown = false;
        /// <summary>
        /// 追踪选中的文本的起始位置
        /// </summary>
        private int _selectionStart = 0;
        /// <summary>
        /// 第一次选中文本的位置
        /// </summary>
        private int _firstSelectionStart = -1;
        /// <summary>
        /// 追踪选中的文本的结束位置
        /// </summary>
        private int _selectionLength = 0;
        #endregion


        private string _text = string.Empty; // "Hello World!";
        [Description("输入的文本"), Category("你的自定义")]
        public override string Text
        {
            get => this._text;
            set
            {
                this._text = value;
                base.OnTextChanged(EventArgs.Empty);// 触发事件
            }
        }
        private int _borderWidth = 1;
        [Description("边框宽度"), Category("你的自定义")]
        public int BorderWidth
        {
            get => _borderWidth; set
            {
                _borderWidth = value;
                Invalidate();
            }
        }
        private Color _borderColor = Color.FromArgb(220, 220, 220);
        [Description("边框颜色"), Category("你的自定义")]
        public Color BorderColor
        {
            get => _borderColor; set
            {
                _borderColor = value;
                Invalidate();
            }
        }

        private bool _isCornerRadius = true;
        [Description("圆角角度"), Category("你的自定义")]
        public virtual bool IsCornerRadius
        {
            get => _isCornerRadius;
            set
            {
                _isCornerRadius = value;
                Invalidate();
            }
        }

        private int _cornerRadius = 50;
        [Description("圆角角度"), Category("你的自定义")]
        public virtual int ConerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(value, 1);
                Invalidate();
            }
        }

        private string _placeholder = "水印文本";
        [Description("水印文本"), Category("你的自定义")]
        public string Placeholder
        {
            get => _placeholder; set
            {
                _placeholder = value;
                this.OnPaint(null);
            }
        }
        private Color _placeholderColor = Color.LightGray;
        [Description("水印文本字体颜色"), Category("你的自定义")]
        public Color PlaceholderColor
        {
            get => _placeholderColor; set
            {
                _placeholderColor = value;
                this.OnPaint(null);
            }
        }

        #region Windows相关处理消息
        // 参考 https://blog.csdn.net/weixin_33709219/article/details/93162243
        /// <summary>
        /// 捕获按键
        /// </summary>
        private const int WM_KEYDOWN = 0x100;
        /// <summary>
        /// 捕获字符输入(未经输入法直接送入程序中的字符会响应的消息)
        /// </summary>
        private const int WM_CHAR = 0x102;
        //private const int WM_CHAR = 0x0102;
        /// <summary>
        /// IME得到了转换结果中的一个字符(由输入法生成的一个字符，这个字符既有可能是单字节字符也有可能是双字节字符。)
        /// </summary>
        private const int WM_IME_CHAR = 0x0286;
        private const int WM_IME_STARTCOMPOSITION = 0x010D;
        /// <summary>
        /// IME根据用户击键的情况更改了按键组合状态
        /// </summary>
        private const int WM_IME_COMPOSITION = 0x010F;
        /// <summary>
        /// 修正当前的编码
        /// </summary>
        private const int GCS_COMPSTR = 0x0008;
        /// <summary>
        /// 修正编码结果串.
        /// </summary>
        private const int GCS_RESULTSTR = 0x0800;
        /// <summary>
        /// 输入焦点转移到了某个窗口上
        /// </summary>
        private const int WM_IME_SETCONTEXT = 0x0281;
        /// <summary>
        /// 窗口重画自己 15
        /// </summary>
        private const int WM_PAINT = 0x000F;
        /// <summary>
        /// 关闭窗口 8
        /// </summary>
        private const int WM_CLOSE = 0x0010;
        /// <summary>
        /// 窗口被销毁7
        /// </summary>
        private const int WM_DESTROY = 0x0002;
        /// <summary>
        ///  以给定的坐标显示窗口，受IME控制
        /// </summary>
        private const int CFS_POINT = 0x0002;
        /// <summary>
        ///  给定的大小显示窗口
        /// </summary>
        //private const int CFS_RECT = 0x0001;
        #endregion

        #region 引入需要的Windows API函数  
        [DllImport("imm32.dll")]
        public static extern bool ImmSetOpenStatus(IntPtr himc, bool b);
        [DllImport("imm32.dll")]
        private static extern bool ImmSetCompositionWindow(IntPtr hwnd, ref COMPOSITIONFORM lpCompForm);
        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);
        [DllImport("Imm32.dll")]
        private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll")]
        static extern int ImmGetCompositionString(IntPtr hIMC, int dwIndex, StringBuilder lpBuf, int dwBufLen);
        #endregion
        /// <summary>
        /// 绑定输入法位置信息等等
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct COMPOSITIONFORM
        {
            public int dwStyle;
            public Point ptCurrentPos;
            public Rectangle rcArea;
        }

        public CustomTextBoxControl()
        {
            this.DoubleBuffered = true; // 避免闪烁  
            this.BackColor = SystemColors.Window; // 设置背景色为窗口色  

            //this.Cursor = Cursors.IBeam; // 设置光标形状为文本形状  

            // 初始化光标闪烁计时器  
            _cursorBlinkTimer = new Timer();
            _cursorBlinkTimer.Interval = 500; // 设置光标闪烁频率为半秒  
            _cursorBlinkTimer.Tick += CursorBlinkTimer_Tick; // 注册计时器Tick事件处理方法  
            this.Size = new System.Drawing.Size(397, 54);//默认大小
            this.Text = string.Empty;
            if (this.DesignMode)
            {
                // 在设计模式下不加载
                _cursorBlinkTimer.Start(); // 开始光标闪烁
            }
        }

        #region 事件
        private void CursorBlinkTimer_Tick(object? sender, EventArgs e)
        {
            _isCursorVisible = !_isCursorVisible; // 切换光标可见性  
            Invalidate(); // 更新光标的显示状态  
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            this.Focus();//点击时获取焦点
            base.OnMouseClick(e);
        }
        protected override void OnGotFocus(EventArgs eventargs)
        {
            base.OnGotFocus(eventargs);
            if (!_cursorBlinkTimer.Enabled)
                _cursorBlinkTimer.Start(); // 开始光标闪烁  
            //ImmSetOpenStatus(_himc, true);//打开IME功能(已经绘制光标，不需要开启系统IME光标)
            _himc = ImmGetContext(this.Handle);//获取与指定窗口相关联的输入环境
        }

        protected override void OnLostFocus(EventArgs eventargs)
        {
            base.OnLostFocus(eventargs);
            _cursorBlinkTimer.Stop(); // 停止光标闪烁  
            _isCursorVisible = false; // 将光标设置为不可见  
            //ImmSetOpenStatus(_himc, false);//关闭IME功能(已经绘制光标，不需要开启系统IME光标)

            _firstSelectionStart = -1;
            _selectionStart = 0;
            _selectionLength = 0;

            Invalidate(); // 更新光标的显示状态  
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isMouseLeftDown = true;
                if (_cursorBlinkTimer.Enabled)
                {
                    _isCursorVisible = false;
                    _cursorBlinkTimer.Stop(); // 关闭光标闪烁  
                }
                _selectionStart = GetCharIndexFromPosition(e.X);  // 获取字符索引  
                Debug.WriteLine("选中的文本的起始位置：" + _selectionStart.ToString());
                UpdateSelection(e.X);
                Focus(); // 给予焦点
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isMouseLeftDown = false;
            if (e.Button == MouseButtons.Left)
            {
                if (!_cursorBlinkTimer.Enabled && Focused)
                {
                    _isCursorVisible = true;
                    _cursorBlinkTimer.Start(); // 开始光标闪烁  
                }
                _firstSelectionStart = -1;
            }
            base.OnMouseUp(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _isMouseLeftDown)
            {
                UpdateSelection(e.X);
            }
            base.OnMouseMove(e);
        }
        #endregion

        #region 绘制及消息处理
        /// <summary>
        /// 重写 OnPaint 方法，自定义绘制控件  
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (e == null)
            {
                //绘制水印
                if (string.IsNullOrEmpty(this._text) && !string.IsNullOrEmpty(this._placeholder))
                {
                    using (Graphics graphics = Graphics.FromHwnd(base.Handle))
                    {
                        if (this._text.Length == 0 && !string.IsNullOrEmpty(this._placeholder))
                        {
                            TextFormatFlags textFormatFlags = TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
                            if (this.RightToLeft == RightToLeft.Yes)
                            {
                                textFormatFlags |= (TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                            }
                            TextRenderer.DrawText(graphics, this._placeholder, this.Font, base.ClientRectangle, _placeholderColor, textFormatFlags);
                        }
                    }
                }
                return;
            }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            base.OnPaint(e);
            // 绘制文本框边框  
            using (var borderPen = new Pen(this._borderColor, this._borderWidth))
            {
                if (this._isCornerRadius)
                {
                    var gPath = new GraphicsPath();
                    var cr = base.ClientRectangle;
                    gPath.AddArc(0, 0, _cornerRadius, _cornerRadius, 180f, 90f);
                    gPath.AddArc(cr.Width - _cornerRadius - 1, 0, _cornerRadius, _cornerRadius, 270f, 90f);
                    gPath.AddArc(cr.Width - _cornerRadius - 1, cr.Height - _cornerRadius - 1, _cornerRadius, _cornerRadius, 0f, 90f);
                    gPath.AddArc(0, cr.Height - _cornerRadius - 1, _cornerRadius, _cornerRadius, 90f, 90f);
                    gPath.CloseFigure();
                    e.Graphics.DrawPath(borderPen, gPath);
                    // 使用 GraphicsPath 创建 Region ,绘制剪辑区域
                    this.Region = new Region(gPath);
                }
                else
                {
                    e.Graphics.DrawRectangle(borderPen, ClientRectangle);
                }
            }
            Size sizeText = GetMeasureText();
            // 绘制选中的文本背景  
            if (_selectionLength > 0 && _selectionLength < this._text.Length - _selectionStart)
            {
                string selectedText = this._text.Substring(_selectionStart, _selectionLength);
                SizeF selectedTextSize = e.Graphics.MeasureString(selectedText, Font);
                float selectedX = e.Graphics.MeasureString(this._text.Substring(0, _selectionStart), Font).Width;
                RectangleF selectionRectangle = new RectangleF(selectedX, Height / 2 - sizeText.Height / 2, selectedTextSize.Width, sizeText.Height);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(128, Color.LightBlue)))
                    e.Graphics.FillRectangle(Brushes.LightBlue, selectionRectangle);
            }
            // 绘制文本内容  
            using (SolidBrush textBrush = new SolidBrush(ForeColor))
            {
                e.Graphics.DrawString(this._text, Font, textBrush, (new Point() { X = 0, Y = Height / 2 - sizeText.Height / 2 }));
                this._lastDrawText = this._text;
            }

            // 绘制光标  
            if (_isCursorVisible && this.Focused)
            {
                int cursorX = sizeText.Width;
                using (Pen cursorPen = new Pen(ForeColor, 1))
                    e.Graphics.DrawLine(cursorPen, cursorX, Height / 2 - sizeText.Height / 2, cursorX, Height / 2 + sizeText.Height / 2);
            }
        }
        /// <summary>
        /// 处理键盘输入，实现文本输入和光标移动等功能  
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        protected override bool ProcessKeyEventArgs(ref Message m)
        {

            switch (m.Msg)
            {
                case WM_KEYDOWN:
                    Keys keyCode = (Keys)m.WParam.ToInt32();
                    switch (keyCode)
                    {
                        case Keys.Left:
                            if (_cursorPosition > 0) _cursorPosition--;
                            Invalidate();
                            return true;// 返回true表示已处理该消息，不再传递给其他处理程序  
                        case Keys.Right:
                            if (_cursorPosition < this._text.Length) _cursorPosition++;
                            Invalidate();
                            return true;
                        case Keys.Back:
                            if (_selectionLength > 0 && _selectionLength < this._text.Length - _selectionStart)//删除选中
                            {
                                this._text = this._text.Remove(_selectionStart, _selectionLength);
                                _cursorPosition -= _selectionLength;
                                //Invalidate();
                                Refresh();//即刻重绘
                                ClearSelected();
                                ClearSelected();//当选中文本后，再次输入时进行清空被选中内容
                            }
                            else if (!string.IsNullOrEmpty(this._text))
                            {
                                this._text = this._text.Remove(this._text.Length - 1, 1);
                                _cursorPosition--;
                                Invalidate();
                            }
                            return true;
                        case Keys.Delete:
                            if (_selectionLength > 0 && _selectionLength < this._text.Length - _selectionStart)//删除选中
                            {
                                this._text = this._text.Remove(_selectionStart, _selectionLength);
                                _cursorPosition -= _selectionLength;
                                //Invalidate();
                                Refresh();//即刻重绘
                                ClearSelected();//当选中文本后，再次输入时进行清空被选中内容
                            }
                            else if (_cursorPosition < this._text.Length)
                            {
                                this._text = this._text.Remove(_cursorPosition, 1);
                                Invalidate();
                            }
                            return true;
                        default:
                            //char typedChar = (char)keyCode;
                            //检查输入的字符是否是字母、数字、标点符号或符号
                            //if (char.IsLetterOrDigit(typedChar))
                            //    this._text = this._text.Insert(cursorPosition, typedChar.ToString());
                            //else if (char.IsSymbol(typedChar))
                            //    this._text = this._text.Insert(cursorPosition, typedChar.ToString());
                            //else if (char.IsPunctuation(typedChar))
                            //    this._text = this._text.Insert(cursorPosition, typedChar.ToString());

                            //HashSet<char> allowedChars = new HashSet<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()");
                            //if (allowedChars.Contains(typedChar)){}

                            //_cursorPosition++;
                            //Invalidate();
                            return true;
                    }
                case WM_CHAR:
                    char _char = (char)m.WParam;
                    if (char.IsControl(_char))
                        return true;

                    byte[] byteArray = new byte[1];
                    byteArray[0] = (byte)_char;
                    this.Text += _char; // 将输入的字符添加到_text中

                    #region 将导致特殊符出现。
                    //StringBuilder str = new StringBuilder();
                    //int size = ImmGetCompositionString(_himc, GCS_COMPSTR, null, 0);
                    //size += sizeof(Char);
                    //ImmGetCompositionString(_himc, GCS_RESULTSTR, str, size);
                    //_text += str.ToString();
                    #endregion

                    _cursorPosition++;
                    Invalidate(); //重新绘制控件，以触发OnPaint方法并显示文本  
                    return true;
                default: break;
            }

            return base.ProcessKeyEventArgs(ref m); // 调用基类的处理方法处理其他消息或未处理的消息。这样，例如Tab键等仍然可以正常工作。
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            switch (m.Msg)
            {
                case WM_IME_SETCONTEXT://把输入与指定的窗口进行关联.
                    //if (m.WParam.ToInt32() == 1)
                    //    ImmAssociateContext(this.Handle, _himc);//奇怪，为什么会导致无法切换中文输入呢？
                    break;
                case WM_KEYDOWN:// 处理按键消息
                    //ProcessKeyEventArgs中处理
                    break;
                case WM_CHAR:// 处理字符输入消息  
                    //ProcessKeyEventArgs中处理
                    break;
                case WM_IME_CHAR:// 处理字符输入消息  
                    //if (m.WParam.ToInt32() == 0x0001/*PM_REMOVE*/) 
                    //ProcessKeyEventArgs中处理
                    break;
                case WM_IME_STARTCOMPOSITION://IME准备生成转换结果
                    // 设置输入法窗口位置  
                    COMPOSITIONFORM compForm = new COMPOSITIONFORM();
                    compForm.dwStyle = CFS_POINT;
                    // 设置输入法窗口的位置为光标的位置
                    var sizeText = GetMeasureText();
                    compForm.ptCurrentPos = new Point(sizeText.Width - 2, Height / 2 - sizeText.Height / 2);
                    var succeed = ImmSetCompositionWindow(_himc, ref compForm);
#if DEBUG
                    Debug.WriteLine("*输入法窗口的位置：" + compForm.ptCurrentPos.ToString());
                    Debug.WriteLine("*输入法窗口的位置是否成功：" + succeed.ToString());
                    Debug.WriteLine("*Win32报错代码：" + Marshal.GetLastWin32Error());

#endif
                    break;
                case WM_IME_COMPOSITION://IME根据用户击键的情况更改了按键组合状态

                    break;

                case WM_PAINT:
                    this.OnPaint(null);
                    break;
                case WM_CLOSE:
                    this.OnPaint(null);
                    break;
                case WM_DESTROY:
                    this.OnPaint(null);
                    break;
                default: break;
            }
        }
        #endregion

        #region 私有方法
        private Size GetMeasureText()
        {
            var measureText = TextRenderer.MeasureText(this._text, Font);
            if (string.IsNullOrWhiteSpace(this._text))
            {
                measureText = TextRenderer.MeasureText("NULL", Font);
                measureText.Width = 5;
            }

            return measureText;
        }
        /// <summary>
        /// 据当前鼠标的位置更新选中的文本
        /// </summary>
        /// <param name="x"></param>
        private void UpdateSelection(int x)
        {
            int pos = GetCharIndexFromPosition(x);  // 需要实现这个方法，它返回在指定位置的字符索引  
            if (pos < 0) pos = 0;
            if (pos > _selectionStart)//从左往右选
                _selectionLength = pos - _selectionStart;
            else if (pos < _selectionStart)//从右往左选
            {
                if (_firstSelectionStart == -1)//锁定位置
                    _firstSelectionStart = _selectionStart;
                int slen = _firstSelectionStart - pos;
                _selectionLength = slen;
                _selectionStart = pos;
            }
            Invalidate();  // 触发 Paint 事件，重绘控件  
        }
        /// <summary>
        /// 返回在指定x位置的字符的索引
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private int GetCharIndexFromPosition(int x)
        {
            if (string.IsNullOrEmpty(Text)) return 0;

            using (var graphics = CreateGraphics())
            {
                for (int i = 0; i < Text.Length; i++)
                {
                    var size = graphics.MeasureString(Text.Substring(0, i), Font).ToSize();

                    if (x <= size.Width)
                        return i;
                }
            }
            // 如果x超过了文本的最后位置，返回文本的长度  
            return Text.Length;
        }
        /// <summary>
        /// 初始化选中
        /// </summary>
        private void ClearSelected() {
            _firstSelectionStart = -1;
            _selectionStart = 0;
            _selectionLength = 0;
        }
        #endregion
    }
}
