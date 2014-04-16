using System;
using System.Text;
using System.Windows.Forms;

using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Security.AccessControl;


namespace FileMD5
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            backgroundWorker1.WorkerReportsProgress = true;//允許BackgroundWorker報告進度

            if (txtResult.Text == "")
                btnVerify.Enabled = false;
        }
        public String HashString(byte[] data)
        {
            string hash = "";
            for (int i = 0; i < data.Length; i++)
                hash += data[i].ToString("X");
            return hash;
        }
        long fileSize = 0;//選擇的檔案大小
        private void btnGetHash_Click(object sender, EventArgs e)
        {
            fileSize = 0;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();//引用stopwatch物件
                sw.Reset();//碼表歸零
                sw.Start();//碼表開始計時
                byte[] md5data = new byte[32];
                byte[] shadata = new byte[64];
                Thread thread1 = new Thread(new ThreadStart(() =>
                    {
                        using (FileStream fs = File.OpenRead(openFileDialog1.FileName))
                        {
                            MD5 md5 = new MD5CryptoServiceProvider();
                            md5data = md5.ComputeHash(fs);

                            fileSize = fs.Length;
                            fs.Close();
                        }
                    }));
                Thread thread2 = new Thread(new ThreadStart(() =>
                   {
                       using (FileStream fs = File.OpenRead(openFileDialog1.FileName))
                       {
                           SHA256Managed sha = new SHA256Managed();
                           shadata = sha.ComputeHash(fs);

                           fileSize = fs.Length;
                           fs.Close();
                       }
                   }));
                thread1.Start(); thread2.Start();
                thread1.Join(); thread2.Join();
                if (thread1.ThreadState == ThreadState.Stopped && thread2.ThreadState == ThreadState.Stopped)
                {
                    sw.Stop();//碼錶停止
                    string result1 = sw.Elapsed.TotalSeconds.ToString();//印出所花費的總豪秒數
                    txtSha1.Text = HashString(shadata);
                    txtMd5.Text = HashString(md5data);
                    txtResult.Text = " 時間 :" + result1;
                }
            }
        }

        int barMaxValue = 0;//進度條的最大值(檔案個數)
        string matchText = "";//符合的檔案
        private void btnVerify_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                txtResult.Text = "";
                barMaxValue = 0;
                matchText = "";
                                
                DirectoryInfo dirInfo = new DirectoryInfo(folderBrowserDialog1.SelectedPath);
                #region 得到資料夾的讀取權限
                DirectorySecurity ds = dirInfo.GetAccessControl();
                FileSystemAccessRule ar1 = new FileSystemAccessRule("Administrators", FileSystemRights.Read, AccessControlType.Allow);
                FileSystemAccessRule ar2 = new FileSystemAccessRule("Administrators", FileSystemRights.Read, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow);

                ds.AddAccessRule(ar1);
                ds.AddAccessRule(ar2);
                dirInfo.SetAccessControl(ds);
                #endregion //得到資料夾的讀取權限

                //barMaxValue = dirInfo.GetFiles("*.*", SearchOption.AllDirectories).Length;
                foreach (FileInfo file in dirInfo.GetFiles("*.*", SearchOption.AllDirectories))
                {
                    try//取得(子)目錄下所有檔案個數
                    {
                        if (dirInfo.Attributes != FileAttributes.Hidden && dirInfo.Attributes != FileAttributes.System)
                            barMaxValue++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
                progressBar1.Maximum = barMaxValue;

                backgroundWorker1.RunWorkerAsync(barMaxValue);//使用RunWorkerAsync方法，觸動DoWork事件
            }
        }

        private void Worker(BackgroundWorker bkWorker)
        {
            int barCurrent = 0;//進度條目前的值

            DirectoryInfo dirInfo = new DirectoryInfo(folderBrowserDialog1.SelectedPath);
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;//比較字串
            String verityBase = txtMd5.Text;//以此字串為基底來比較其他

            //foreach (FileInfo file in dirInfo.GetFiles("*.*", SearchOption.AllDirectories))
            Parallel.ForEach(dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories), file =>
            {
                try
                {
                    if (dirInfo.Attributes != FileAttributes.Hidden && dirInfo.Attributes != FileAttributes.System)
                        using (FileStream fs = File.OpenRead(file.FullName.ToString()))
                        {
                            if (fs.Length == fileSize)//如果找到的檔案與基底檔案的大笑不同，就可以跳過了
                            {
                                MD5 md5 = new MD5CryptoServiceProvider();
                                byte[] md5data = md5.ComputeHash(fs);
                                fs.Close();

                                if (comparer.Compare(verityBase, HashString(md5data)) == 0)//比對結果相符，則顯示檔案位置
                                    matchText += file.FullName.ToString() + "\r\n";
                            }
                            barCurrent++;//進度條目前的值
                            bkWorker.ReportProgress(barCurrent, barCurrent.ToString());//執行ReportProgress方法，觸發ProgressChanged事件
                        }
                    //Thread.Sleep(1);//防止更新太快無法顯示
                    Application.DoEvents();
                }
                catch (Exception ex)
                {
                    //系統的"另一個程序正在使用檔案"
                    //MessageBox.Show("Error: " + ex.Message);
                }
            });
        }
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            //允許BackgroundWorker報告進度
            backgroundWorker1.WorkerReportsProgress = true;

            progressBar1.Maximum = barMaxValue;

            //在方法中傳遞BackgroundWorker參數
            Worker(sender as BackgroundWorker);
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {//回傳執行結果
            try
            {
                txtResult.Text = "正在處理" + e.UserState.ToString() + " / " + progressBar1.Maximum.ToString();
                txtResult.Update();

                progressBar1.Maximum = barMaxValue;
                progressBar1.Value = e.ProgressPercentage;

                //Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (matchText == "")
                txtResult.Text = "共 " + barMaxValue.ToString() + " 個檔案\r\n" + "沒有符合的";
            else
                txtResult.Text = "共 " + barMaxValue.ToString() + " 個檔案\r\n" + "符合的有 :\r\n" + matchText;
        }

        private void txtMd5_TextChanged(object sender, EventArgs e)
        {
            btnVerify.Enabled = true;
        }



    }
}
