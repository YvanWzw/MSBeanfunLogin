using BeanfunLogin;
using CSharpAnalytics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace BeanfunLogin

{
    enum LoginMethod : int
    {
        Regular = 0,
        Keypasco = 1,
        PlaySafe = 2,
        QRCode = 3
    };


    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        private AccountManager accountManager = null;

        public BeanfunClient bfClient;

        public BeanfunClient.QRCodeClass qrcodeClass;

        private string service_code = "610074", service_region = "T9", service_name = "";

        public List<GameService> gameList = new List<GameService>();

        private CSharpAnalytics.Activities.AutoTimedEventActivity timedActivity = null;

        private Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

        private GamePathDB gamePaths = new GamePathDB();

        BackgroundWorker pingWorker = new BackgroundWorker();

        BackgroundWorker loginWorker = new BackgroundWorker();

        BackgroundWorker getOtpWorker = new BackgroundWorker();

        private int jingpingguo = 0;


        public class GameService
        {
            public string name { get; set; }
            public string service_code { get; set; }
            public string service_region { get; set; }

            public GameService(string name, string service_code, string service_region)
            {
                this.name = name;
                this.service_code = service_code;
                this.service_region = service_region;
            }
        }

        public MainWindow()
        {

            if (Properties.Settings.Default.GAEnabled)
            {
                try
                {
                    AutoMeasurement.Instance = new WinFormAutoMeasurement();
                    AutoMeasurement.DebugWriter = d => Debug.WriteLine(d);
                    AutoMeasurement.Start(new MeasurementConfiguration("UA-75983216-4", Assembly.GetExecutingAssembly().GetName().Name, currentVersion.ToString()));
                }
                catch
                {
                    this.timedActivity = null;
                    Properties.Settings.Default.GAEnabled = false;
                    Properties.Settings.Default.Save();
                }
            }

            //TODO:注意关闭时登出
            /*            this.FormClosing += new FormClosingEventHandler((sender, e) => {
                            if (this.bfClient != null) this.bfClient.Logout();
                        });*/

            timedActivity = new CSharpAnalytics.Activities.AutoTimedEventActivity("FormLoad", Properties.Settings.Default.loginMethod.ToString());
            InitializeComponent();
            init();

            if (Properties.Settings.Default.GAEnabled && this.timedActivity != null)
            {
                AutoMeasurement.Client.Track(this.timedActivity);
                this.timedActivity = null;
            }

        }

        public bool init()
        {
            try
            {
                this.bfClient = null;
                this.accountManager = new AccountManager();

                bool res = accountManager.init();
                if (res == false)
                    errexit("帳號記錄初始化失敗，未知的錯誤。", 0);
                refreshAccountList();
                // Properties.Settings.Default.Reset(); //SetToDefault.                  

                // Handle settings.
                if (Properties.Settings.Default.rememberAccount == true)
                    this.tb_account.Text = Properties.Settings.Default.AccountID;
                if (Properties.Settings.Default.rememberPwd == true)
                {
                    /* this.rememberAccount.Enabled = false;*/
                    // Load password.
                    if (File.Exists("UserState.dat"))
                    {
                        try
                        {
                            Byte[] cipher = File.ReadAllBytes("UserState.dat");
                            string entropy = Properties.Settings.Default.entropy;
                            byte[] plaintext = ProtectedData.Unprotect(cipher, Encoding.UTF8.GetBytes(entropy), DataProtectionScope.CurrentUser);
                            this.pb_password.Password = System.Text.Encoding.UTF8.GetString(plaintext);
                        }
                        catch
                        {
                            File.Delete("UserState.dat");
                        }
                    }
                }
                if (Properties.Settings.Default.autoLogin == true)
                {
                    /*  this.UseWaitCursor = true;
                      this.panel2.Enabled = false;
                      this.loginButton.Text = "請稍後...";
                      this.loginWorker.RunWorkerAsync(Properties.Settings.Default.loginMethod);*/
                }
                if (gamePaths.Get("新楓之谷") == "")
                {
                    /* ModifyRegistry myRegistry = new ModifyRegistry();
                     myRegistry.BaseRegistryKey = Registry.CurrentUser;
                     myRegistry.SubKey = "Software\\Gamania\\MapleStory";
                     if (myRegistry.Read("Path") != "")
                     {
                         gamePaths.Set("新楓之谷", myRegistry.Read("Path"));
                         gamePaths.Save();
                     }*/
                }

                this.cb_login_method.SelectedIndex = Properties.Settings.Default.loginMethod;
                /*                this.textBox3.Text = "";

                                if (this.tb_account.Text == "")
                                    this.ActiveControl = this.tb_account;
                                else if (this.pb_password.Password == "")
                                    this.ActiveControl = this.pb_password;*/


                this.getOtpWorker.WorkerReportsProgress = true;
                this.getOtpWorker.WorkerSupportsCancellation = true;
                this.getOtpWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.getOtpWorker_DoWork);
                this.getOtpWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.getOtpWorker_RunWorkerCompleted);
                this.loginWorker.WorkerReportsProgress = true;
                this.loginWorker.WorkerSupportsCancellation = true;
                this.loginWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.loginWorker_DoWork);
                this.loginWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.loginWorker_RunWorkerCompleted);
                this.pingWorker.WorkerReportsProgress = true;
                this.pingWorker.WorkerSupportsCancellation = true;
                this.pingWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.pingWorker_DoWork);
                this.pingWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.pingWorker_RunWorkerCompleted);

                return true;
            }
            catch (Exception e)
            {
                return errexit("初始化失敗，未知的錯誤。" + e.Message, 0);
            }
        }

        public bool errexit(string msg, int method, string title = null)
        {
            string originalMsg = msg;
            if (Properties.Settings.Default.GAEnabled)
                AutoMeasurement.Client.TrackException(msg);

            switch (msg)
            {
                case "LoginNoResponse":
                    msg = "初始化失敗，請檢查網路連線。";
                    method = 0;
                    break;
                case "LoginNoSkey":
                    method = 0;
                    break;
                case "LoginNoAkey":
                    msg = "登入失敗，帳號或密碼錯誤。";
                    break;
                case "LoginNoAccountMatch":
                    msg = "登入失敗，無法取得帳號列表。";
                    break;
                case "LoginNoAccount":
                    msg = "找不到遊戲帳號。";
                    break;
                case "LoginNoResponseVakten":
                    msg = "登入失敗，與伺服器驗證失敗，請檢查是否安裝且已執行vakten程式。";
                    break;
                case "LoginUnknown":
                    msg = "登入失敗，請稍後再試";
                    method = 0;
                    break;
                case "OTPNoLongPollingKey":
                    if (Properties.Settings.Default.loginMethod == (int)LoginMethod.PlaySafe)
                        msg = "密碼獲取失敗，請檢查晶片卡是否插入讀卡機，且讀卡機運作正常。\n若仍出現此訊息，請嘗試重新登入。";
                    else
                    {
                        msg = "已從伺服器斷線，請重新登入。";
                        method = 1;
                    }
                    break;
                case "LoginNoReaderName":
                    msg = "登入失敗，找不到晶片卡或讀卡機，請檢查晶片卡是否插入讀卡機，且讀卡機運作正常。\n若還是發生此情形，請嘗試重新登入。";
                    break;
                case "LoginNoCardType":
                    msg = "登入失敗，晶片卡讀取失敗。";
                    break;
                case "LoginNoCardId":
                    msg = "登入失敗，找不到讀卡機。";
                    break;
                case "LoginNoOpInfo":
                    msg = "登入失敗，讀卡機讀取失敗。";
                    break;
                case "LoginNoEncryptedData":
                    msg = "登入失敗，晶片卡讀取失敗。";
                    break;
                case "OTPUnknown":
                    msg = "獲取密碼失敗，請嘗試重新登入。";
                    break;
                case "LoginNoPSDriver":
                    msg = "PlaySafe驅動初始化失敗，請檢查PlaySafe元件是否已正確安裝。";
                    break;
                default:
                    break;
            }

            MessageBox.Show(msg, title);
            if (method == 0)
                Application.Current.Shutdown();
            else if (method == 1)
            {
                BackToLogin();
            }

            return false;
        }

        public void BackToLogin()
        {
            //隐藏账号列表
            lb_accountList.Visibility = Visibility.Collapsed;
            //显示登陆界面
            grid_login.Visibility = Visibility.Visible;

            Properties.Settings.Default.autoLogin = false;
            init();
            cb_login_method_SelectionChanged(null, null);

            /*           for (int i = 0; i < accountList.Items.Count; ++i)
                       {
                           if ((string)accountList.Items[i] == tb_account.Text)
                           {
                               accountList.SelectedIndex = i;
                               break;
                           }
                       }*/
        }

        private void refreshAccountList()
        {
            string[] accArray = accountManager.getAccountList();
            lb_accountList.Items.Clear();
            lb_accountList.Items.Add(accArray);
        }


        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void cb_login_method_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*   qrCheckLogin.Enabled = false;


               passLabel.Visible = true;
               passwdInput.Visible = true;

               useNewQRCode.Visible = false;
               qrcodeImg.Visible = false;

               rememberAccount.Visible = true;
               rememberAccPwd.Visible = true;
               checkBox3.Visible = true;
               loginButton.Visible = true;

               wait_qrWorker_notify.Visible = false;

               this.gamaotp_challenge_code_output.Text = "";

               Properties.Settings.Default.loginMethod = this.cb_login_methon.SelectedIndex;

               if (Properties.Settings.Default.loginMethod == (int)LoginMethod.QRCode)
               {
                   accountInput.Visible = false;
                   accountLabel.Visible = false;

                   passLabel.Visible = false;
                   passwdInput.Visible = false;

                   useNewQRCode.Visible = true;
                   qrcodeImg.Visible = true;

                   rememberAccount.Visible = false;
                   rememberAccPwd.Visible = false;
                   checkBox3.Visible = false;
                   loginButton.Visible = false;
                   qrcodeImg.Image = null;
                   wait_qrWorker_notify.Text = "取得QRCode中 請稍後";
                   wait_qrWorker_notify.Visible = true;

                   this.qrWorker.RunWorkerAsync(!useNewQRCode.Checked);
                   this.loginMethodInput.Enabled = false;
               }
               else
               {
                   this.passLabel.Text = "密碼";
               }*/
        }



        private void btn_login_Click(object sender, RoutedEventArgs e)
        {

            if (this.pingWorker.IsBusy)
            {
                this.pingWorker.CancelAsync();
            }
            /*  if (this.rememberAccount.Checked == true)
                  Properties.Settings.Default.AccountID = this.accountInput.Text;
              if (this.rememberAccPwd.Checked == true)
              {
                  using (BinaryWriter writer = new BinaryWriter(File.Open("UserState.dat", FileMode.Create)))
                  {
                      // Create random entropy of 8 characters.
                      var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                      var random = new Random();
                      string entropy = new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());

                      Properties.Settings.Default.entropy = entropy;
                      writer.Write(ciphertext(this.passwdInput.Text, entropy));
                  }
              }
              else
              {
                  Properties.Settings.Default.entropy = "";
                  File.Delete("UserState.dat");
              }*/
            Properties.Settings.Default.Save();

            /*
                        this.UseWaitCursor = true;
            */



            this.btn_login.Content = "請稍後...";
            if (Properties.Settings.Default.GAEnabled)
            {
                timedActivity = new CSharpAnalytics.Activities.AutoTimedEventActivity("Login", Properties.Settings.Default.loginMethod.ToString());
                AutoMeasurement.Client.TrackEvent("Login" + Properties.Settings.Default.loginMethod.ToString(), "Login");
            }
            this.loginWorker.RunWorkerAsync(Properties.Settings.Default.loginMethod);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)

                this.DragMove();
        }

        private void btn_win_login_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (grid_login.Visibility == Visibility.Collapsed)
                grid_login.Visibility = Visibility.Visible;
            else
                grid_login.Visibility = Visibility.Collapsed;

        }


        private string otp;

        // Login do work.
        private void loginWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (this.pingWorker.IsBusy)
                Thread.Sleep(137);
            Debug.WriteLine("loginWorker starting");
            if (Thread.CurrentThread.Name != null)
                Thread.CurrentThread.Name = "Login Worker";
            e.Result = "";
            try
            {
                if (Properties.Settings.Default.loginMethod != (int)LoginMethod.QRCode)
                    this.bfClient = new BeanfunClient();
                this.Dispatcher.Invoke(new Action(delegate
                {
                    this.bfClient.Login(this.tb_account.Text, this.pb_password.Password, Properties.Settings.Default.loginMethod, this.qrcodeClass, this.service_code, this.service_region);

                }));
                if (this.bfClient.errmsg != null)
                    e.Result = this.bfClient.errmsg;
                else
                    e.Result = null;
            }
            catch (Exception ex)
            {
                e.Result = "登入失敗，未知的錯誤。\n\n" + ex.Message + "\n" + ex.StackTrace;
            }
        }

        // Login completed.
        private void loginWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (Properties.Settings.Default.GAEnabled && this.timedActivity != null)
            {
                AutoMeasurement.Client.Track(this.timedActivity);
                this.timedActivity = null;
            }
            if (Properties.Settings.Default.keepLogged && !this.pingWorker.IsBusy)
                this.pingWorker.RunWorkerAsync();
            Debug.WriteLine("loginWorker end");
            /*            this.panel2.Enabled = true;
                        this.UseWaitCursor = false;
                        this.loginButton.Text = "登入";*/
            grid_login.Visibility = Visibility.Collapsed;

            if (e.Error != null)
            {
                errexit(e.Error.Message, 1);
                return;
            }
            if ((string)e.Result != null)
            {
                errexit((string)e.Result, 1);
                return;
            }

            try
            {
                redrawSAccountList();

                // Handle panel switching.
                /*             this.ActiveControl = null;*/
                /*                this.Size = new System.Drawing.Size(300, this.Size.Height);
                                this.panel2.SendToBack();
                                this.panel1.BringToFront();*/
                /*                this.AcceptButton = this.btn_getOpt;*/

                //如果账号列表UI够长，就选择默认选中的那个索引
                if (Properties.Settings.Default.autoSelectIndex < this.lv_opt_account.Items.Count)
                    this.lv_opt_account.SelectedIndex = Properties.Settings.Default.autoSelectIndex;
                /*     this.lv_opt_account.Select();*/
                if (Properties.Settings.Default.autoSelect == true && Properties.Settings.Default.autoSelectIndex < this.bfClient.accountList.Count())
                {
                    if (this.pingWorker.IsBusy)
                    {
                        this.pingWorker.CancelAsync();
                    }
                    this.tb_opt_pwd.Text = "獲取密碼中...";
                    this.lv_opt_account.IsEnabled = false;
                    this.btn_getOpt.IsEnabled = false;
                    timedActivity = new CSharpAnalytics.Activities.AutoTimedEventActivity("GetOTP", Properties.Settings.Default.loginMethod.ToString());
                    if (Properties.Settings.Default.GAEnabled)
                    {
                        AutoMeasurement.Client.TrackEvent("GetOTP" + Properties.Settings.Default.loginMethod.ToString(), "GetOTP");
                    }
                    this.getOtpWorker.RunWorkerAsync(Properties.Settings.Default.autoSelectIndex);
                }
                if (Properties.Settings.Default.keepLogged && !this.pingWorker.IsBusy)
                    this.pingWorker.RunWorkerAsync();
                /*                ShowToolTip(listView1, "步驟1", "選擇欲開啟的遊戲帳號，雙擊以複製帳號。");
                                ShowToolTip(getOtpButton, "步驟2", "按下以在右側產生並自動複製密碼，至遊戲中貼上帳密登入。");
                                Tip.SetToolTip(getOtpButton, "點擊獲取密碼");
                                Tip.SetToolTip(listView1, "雙擊即自動複製");
                                Tip.SetToolTip(textBox3, "點擊一次即自動複製");*/
                Properties.Settings.Default.showTip = false;
                Properties.Settings.Default.Save();
            }
            catch
            {
                errexit("登入失敗，無法取得帳號列表。", 1);
            }

        }

        private void redrawSAccountList()
        {
            lv_opt_account.Items.Clear();
            foreach (var account in this.bfClient.accountList)
            {
                string[] row = { WebUtility.HtmlDecode(account.sname), account.sacc };
                lv_opt_account.Items.Add(new { AccName = row[0], GamAcc = row[1] });
            }
        }

        // getOTP do work.
        private void getOtpWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (this.pingWorker.IsBusy)
                Thread.Sleep(133);
            Debug.WriteLine("getOtpWorker start");
            Thread.CurrentThread.Name = "GetOTP Worker";
            int index = (int)e.Argument;
            e.Result = index;
            Debug.WriteLine("Count = " + this.bfClient.accountList.Count + " | index = " + index);
            if (this.bfClient.accountList.Count <= index)
            {
                return;
            }
            Debug.WriteLine("call GetOTP");
            this.otp = this.bfClient.GetOTP(Properties.Settings.Default.loginMethod, this.bfClient.accountList[index], this.service_code, this.service_region);
            Debug.WriteLine("call GetOTP done");
            if (this.otp == null)
            {
                e.Result = -1;
                return;
            }

            if (false == Properties.Settings.Default.opengame)
            {
                Debug.WriteLine("no open game");
                return;
            }

            string procPath = gamePaths.Get(service_name);
            string sacc = this.bfClient.accountList[index].sacc;
            string otp = new string(this.otp.Where(c => char.IsLetter(c) || char.IsDigit(c)).ToArray());

            if (!File.Exists(procPath))
                return;

            if (Properties.Settings.Default.GAEnabled)
            {
                try
                {
                    AutoMeasurement.Client.TrackEvent(System.IO.Path.GetFileName(procPath), "processName");
                }
                catch
                {
                    Debug.WriteLine("invalid path:" + procPath);
                }
            }

            if (procPath.Contains("elsword.exe"))
            {
                processStart(procPath, sacc + " " + otp + " TW");
            }
            else if (procPath.Contains("KartRider.exe"))
            {
                processStart(procPath, "-id:" + sacc + " -password:" + otp + " -region:1");
            }
            else if (procPath.Contains("mabinogi.exe"))
            {
                processStart(procPath, "/N:" + sacc + " /V:" + otp + " /T:gamania");
            }
            else // fallback to default strategy
            {
                if (procPath.Contains("MapleStory.exe"))
                {
                    foreach (Process process in Process.GetProcesses())
                    {
                        if (process.ProcessName == "MapleStory")
                        {
                            Debug.WriteLine("find game");
                            return;
                        }
                    }
                }

                processStart(procPath, "tw.login.maplestory.gamania.com 8484 BeanFun " + sacc + " " + otp);
            }


            return;
        }

        private void processStart(string prog, string arg)
        {
            try
            {
                Debug.WriteLine("try open game");
                ProcessStartInfo psInfo = new ProcessStartInfo();
                psInfo.FileName = prog;
                psInfo.Arguments = arg;
                psInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(prog);
                Process.Start(psInfo);
                Debug.WriteLine("try open game done");
            }
            catch
            {
                errexit("啟動失敗，請嘗試手動以系統管理員身分啟動遊戲。", 2);
            }
        }

        // getOTP completed.
        private void getOtpWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (Properties.Settings.Default.GAEnabled && this.timedActivity != null)
            {
                AutoMeasurement.Client.Track(this.timedActivity);
                this.timedActivity = null;
            }

            const int VK_TAB = 0x09;
            const byte VK_CONTROL = 0x11;
            const int VK_V = 0x56;
            const int VK_ENTER = 0x0d;
            const byte KEYEVENTF_EXTENDEDKEY = 0x1;
            const byte KEYEVENTF_KEYUP = 0x2;

            Debug.WriteLine("getOtpWorker end");
            this.btn_getOpt.Content = "獲取密碼";
            this.lv_opt_account.IsEnabled = true;
            this.btn_getOpt.IsEnabled = true;

            if (e.Error != null)
            {
                this.tb_opt_pwd.Text = "獲取失敗";
                errexit(e.Error.Message, 2);
                return;
            }
            int index = (int)e.Result;

            if (index == -1)
            {
                this.tb_opt_pwd.Text = "獲取失敗";
                errexit(this.bfClient.errmsg, 2);
            }
            else
            {
                int accIndex = lv_opt_account.SelectedIndex;
                string acc = this.bfClient.accountList[index].sacc;
                try
                {
                    Clipboard.SetText(acc);
                }
                catch
                {
                    return;
                }

                IntPtr hWnd;
                /*     if (autoPaste.Checked == true && (hWnd = WindowsAPI.FindWindow(null, "MapleStory")) != IntPtr.Zero)
                     {
                         WindowsAPI.SetForegroundWindow(hWnd);

                         WindowsAPI.keybd_event(VK_CONTROL, 0x9d, KEYEVENTF_EXTENDEDKEY, 0);
                         WindowsAPI.keybd_event(VK_V, 0x9e, 0, 0);
                         Thread.Sleep(200);
                         WindowsAPI.keybd_event(VK_V, 0x9e, KEYEVENTF_KEYUP, 0);
                         Thread.Sleep(200);
                         WindowsAPI.keybd_event(VK_CONTROL, 0x9d, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

                         WindowsAPI.keybd_event(VK_TAB, 0, KEYEVENTF_EXTENDEDKEY, 0);
                         WindowsAPI.keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0);
                     }*/

                this.tb_opt_pwd.Text = this.otp.Substring(0,10);
                try
                {
                    Clipboard.SetText(tb_opt_pwd.Text);
                }
                catch
                {
                    return;
                }

                Thread.Sleep(250);

                /*  if (autoPaste.Checked == true && (hWnd = WindowsAPI.FindWindow(null, "MapleStory")) != IntPtr.Zero)
                  {
                      WindowsAPI.keybd_event(VK_CONTROL, 0x9d, KEYEVENTF_EXTENDEDKEY, 0);
                      WindowsAPI.keybd_event(VK_V, 0x9e, 0, 0);
                      Thread.Sleep(200);
                      WindowsAPI.keybd_event(VK_V, 0x9e, KEYEVENTF_KEYUP, 0);
                      Thread.Sleep(200);
                      WindowsAPI.keybd_event(VK_CONTROL, 0x9d, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

                      WindowsAPI.keybd_event(VK_ENTER, 0, 0, 0);
                      WindowsAPI.keybd_event(VK_ENTER, 0, KEYEVENTF_KEYUP, 0);

                      listView1.Items[accIndex].BackColor = Color.Green;
                      listView1.Items[accIndex].Selected = false;
                  }*/


            }

            if (Properties.Settings.Default.keepLogged && !this.pingWorker.IsBusy)
                this.pingWorker.RunWorkerAsync();
        }

        // Ping to Beanfun website.每分钟ping一次以防断开连接
        private void pingWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.Name = "ping Worker";
            Debug.WriteLine("pingWorker start");
            const int WaitSecs = 60; // 1min

            while (Properties.Settings.Default.keepLogged)
            {
                if (this.pingWorker.CancellationPending)
                {
                    Debug.WriteLine("break duo to cancel");
                    break;
                }

                if (this.getOtpWorker.IsBusy || this.loginWorker.IsBusy)
                {
                    Debug.WriteLine("ping.busy sleep 1s");
                    System.Threading.Thread.Sleep(1000 * 1);
                    continue;
                }

                if (this.bfClient != null)
                    this.bfClient.Ping();

                for (int i = 0; i < WaitSecs; ++i)
                {
                    if (this.pingWorker.CancellationPending)
                        break;
                    System.Threading.Thread.Sleep(1000 * 1);
                }
            }
        }

        private void pingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Debug.WriteLine("ping.done");
        }

        private void qrWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.bfClient = new BeanfunClient();
            string skey = this.bfClient.GetSessionkey();
            this.qrcodeClass = this.bfClient.GetQRCodeValue(skey, (bool)e.Argument);
        }

        private void btn_getOpt_Click(object sender, RoutedEventArgs e)
        {
            if (this.pingWorker.IsBusy)
            {
                this.pingWorker.CancelAsync();
            }
            if (lv_opt_account.SelectedItems.Count <= 0 || this.loginWorker.IsBusy) return;
            if (Properties.Settings.Default.autoSelect == true)
            {
                Properties.Settings.Default.autoSelectIndex = lv_opt_account.SelectedIndex;
                Properties.Settings.Default.Save();
            }

            this.tb_opt_pwd.Text = "獲取密碼中...";
            this.lv_opt_account.IsEnabled = false;
            this.btn_getOpt.IsEnabled = false;
            /*   this.comboBox2.Enabled = false;*/
            if (Properties.Settings.Default.GAEnabled)
            {
                timedActivity = new CSharpAnalytics.Activities.AutoTimedEventActivity("GetOTP", Properties.Settings.Default.loginMethod.ToString());
                AutoMeasurement.Client.TrackEvent("GetOTP" + Properties.Settings.Default.loginMethod.ToString(), "GetOTP");
            }
            this.getOtpWorker.RunWorkerAsync(lv_opt_account.SelectedIndex);
        }

        private void btn_jingpingguo_Click(object sender, RoutedEventArgs e)
        {
            jingpingguo++;
            lb_jingpingguo.Content = "消耗的金苹果数：" + jingpingguo;
            bmp_coujiang.Visibility = Visibility.Visible;
            Random rd = new Random();
            int next = rd.Next(300);
            if (next == 2 || next == 9 || next == 17 || next == 25 || next == 69)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/lunhui.png", UriKind.Relative));
            else if( next ==5 || next ==11 || next ==19 || next ==36)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/kuxing.png", UriKind.Relative));
            else if( next ==85)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/ranshao.png", UriKind.Relative));
            else if (next == 124)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/shuanglian.png", UriKind.Relative));
            else if (next == 328)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/weiershu.png", UriKind.Relative));
            else if (next>328)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/xinzhang.png", UriKind.Relative));
            else if (next <328&&next>124)
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/qihuan.png", UriKind.Relative));
            else
                bmp_coujiang.Source = new BitmapImage(new Uri("Resources/jiqiren.png", UriKind.Relative));

        }


        private void qrWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.cb_login_method.IsEnabled = true;
            /*   wait_qrWorker_notify.Visible = false;
               if (this.qrcodeClass == null)
                   wait_qrWorker_notify.Text = "QRCode取得失敗";
               else
               {
                   qrcodeImg.Image = qrcodeClass.bitmap;
                   qrCheckLogin.Enabled = true;
               }*/
        }

        private void qrCheckLogin_Tick(object sender, EventArgs e)
        {
            if (this.qrcodeClass == null)
            {
                MessageBox.Show("QRCode not get yet");
                return;
            }
            int res = this.bfClient.QRCodeCheckLoginStatus(this.qrcodeClass);
            /*            if (res != 0)
                            this.qrCheckLogin.Enabled = false;
                        if (res == 1)
                        {
                            loginButton_Click(null, null);
                        }
                        if (res == -2)
                        {
                            comboBox1_SelectedIndexChanged(null, null);
                        }*/
        }
    }
}

