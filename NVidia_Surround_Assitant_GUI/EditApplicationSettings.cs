﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Xml;
using NLog;
using System.Xml.Serialization;
using System.Drawing.Imaging;

namespace NVidia_Surround_Assistant
{
    //TODO cancel search and restart if search is pressed while searching
    public partial class EditApplicationSettings : Form
    {
        //Logger
        Logger logger = LogManager.GetLogger("nvsaLogger");

        //gameList created from game database api
        List<ApplicationInfo> gameList_UserChoice = new List<ApplicationInfo>();
        
        public ApplicationInfo AppInfo;
                
        //Timer variables
        int timerTickCount = 0;

        int spacing = 40;
        int y_spacing;

        //Application Database api  interfaces
        IgdbAPI.ApiClient igdbClient = new IgdbAPI.ApiClient("https://api-2445582011268.apicast.io");

        //New app flag
        bool autoSearchNewApp;
        
        public EditApplicationSettings(ApplicationInfo Appinfo, bool AutoSearchNewApp)
        {
            InitializeComponent();

            if (gameList_UserChoice.Count == 0)
            {
                AppInfo = Appinfo;
                gameList_UserChoice.Add(Appinfo);
                //Update all textboxes
                textBoxGameSearch.Text = Appinfo.DisplayName;
            }
            
            autoSearchNewApp = AutoSearchNewApp;
            if(!autoSearchNewApp)
            {
                pictureBoxDisabled.Visible = !AppInfo.Enabled;
                pictureBoxEnabled.Visible = AppInfo.Enabled;
            }
                        
            //Setup Rest client for IGDB
            igdbClient.SetAuthHeader("user-key", "ac7be2d5cce384506dfc786fdabec51c");
        }

        private void UpdateDisplay(ApplicationInfo Appinfo)
        {
            if (Appinfo.DisplayName != null)
                textBoxDisplayName.Text = Appinfo.DisplayName;
            if(Appinfo.FullPath != null)
                textBoxAppPath.Text = Appinfo.FullPath;

            if (Appinfo.Image != null)
                pictureBoxGameBoxCover.Image = Appinfo.Image;
            else
                pictureBoxGameBoxCover.Image = NVidia_Surround_Assistant.Properties.Resources.delete_50x50;
        }

        private void ToggleWait(bool waitDisabled)
        {
            //show wait label
            labelWait.Visible = !waitDisabled;
            //set the timer to the correct state
            timerWait.Enabled = !waitDisabled;

            //Hide rest of the controls
            label2.Visible = waitDisabled;
            label3.Visible = waitDisabled;
            label4.Visible = waitDisabled;
            label5.Visible = waitDisabled;

            pictureBoxDeleteImage.Visible = waitDisabled;
            pictureBoxEditImage.Visible = waitDisabled;
            pictureBoxDisabled.Visible = waitDisabled && !AppInfo.Enabled;
            pictureBoxEnabled.Visible = waitDisabled && AppInfo.Enabled;
            pictureBoxGameBoxCover.Visible = waitDisabled;
            pictureBoxChangeFileLocation.Visible = waitDisabled;

            textBoxAppPath.Visible = waitDisabled;
            textBoxDisplayName.Visible = waitDisabled;

            if (waitDisabled)
            {                
                pictureBoxApply.Image = NVidia_Surround_Assistant.Properties.Resources.success_green_25x25;
                pictureBoxApply.Location = new Point(88, 310);
                pictureBoxCancel.Location = new Point(163, 310);

                Width = pictureBoxGameBoxCover.Right + spacing;
                Height = pictureBoxGameBoxCover.Bottom + y_spacing;
            }
            else
            {               
                pictureBoxApply.Image = NVidia_Surround_Assistant.Properties.Resources.success_25x25;
                pictureBoxApply.Location = new Point(88, labelWait.Bottom + spacing);
                pictureBoxCancel.Location = new Point(163, labelWait.Bottom + spacing);

                Width = pictureBoxSearch.Right + spacing;
                Height = pictureBoxCancel.Bottom + y_spacing;
            }          
        }

        #region APIinterface
        private async void GetGameList()
        {
            ToggleWait(false);
            await IGDB_GetGameList();
            for (int i = 0; i < 1000; i++)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(1);
            }

            comboBoxGameList.SuspendLayout();
            comboBoxGameList.SelectedIndexChanged -= comboBoxGameList_SelectedIndexChanged;
            comboBoxGameList.DataSource = gameList_UserChoice;
            comboBoxGameList.DisplayMember = "DisplayName";            
            comboBoxGameList.SelectedIndexChanged += comboBoxGameList_SelectedIndexChanged;            
            comboBoxGameList.ResumeLayout();
            if (comboBoxGameList.Items.Count > 0)
            {
                comboBoxGameList.SelectedIndex = 0;
                UpdateDisplay((ApplicationInfo)comboBoxGameList.SelectedItem);
            }            
            ToggleWait(true);
        }
        #endregion

        #region http
        private string GetHttpPage(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private async Task<Bitmap> GetHttpImg(string uri)
        {
            Bitmap cover;
            try
            {   
                if(uri.StartsWith("//"))
                {
                    uri = "https:" + uri;
                }
                uri = uri.Replace("t_thumb", "t_cover_big");

                using (WebClient client = new WebClient())
                {
                    using (Stream stream = await client.OpenReadTaskAsync(new Uri(uri)))
                    {
                        if (stream != null)
                        {
                            cover = new Bitmap(Bitmap.FromStream(stream));
                            //compress image if required
                            if (CheckImageSize(cover, 20971520))
                                return cover;
                            else
                                return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if(logger != null)
                    logger.Debug("Edit Application: {0} | URI: {1}", ex.Message, uri);
            }
            return null;
        }
        #endregion
                
        #region IGDB
        private async Task IGDB_GetGameList()
        {
            try
            {
                Task<List<IgdbAPI.Game>> getGameList = igdbClient.SearchGamesAsync(textBoxGameSearch.Text);
                List<IgdbAPI.Game> gameList = await getGameList;
                ApplicationInfo appInfo;

                if (gameList != null)
                {
                    foreach (IgdbAPI.Game game in gameList)
                    {
                        if (logger != null)
                            logger.Info("Game found: {0}", game.name);
                        appInfo = new ApplicationInfo
                        {
                            DisplayName = game.name,
                        };

                        if (game.cover != null)
                        {
                            try
                            {
                                appInfo.Image = await GetHttpImg(game.cover.url);
                            }
                            catch (System.NullReferenceException)
                            {
                                appInfo.Image = null;
                            }                            
                        }
                        if (appInfo != null)
                            gameList_UserChoice.Add(appInfo);
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                logger.Info("Application Settings: {0}", ex.Message);
            }
        }

        #endregion

        private bool CheckImageSize(Bitmap img, long sizeRequired)
        {
            using (var mss = new MemoryStream())
            {
                img.Save(mss, ImageFormat.Png);
                if (mss.Length > sizeRequired)
                    return false;
                else
                    return true;
            }                       
        }

        private void timerWait_Tick(object sender, EventArgs e)
        {
            switch(timerTickCount % 4)
            {
                case 0:
                    labelWait.Text = ".";
                    break;
                case 1:
                    labelWait.Text = "..";
                    break;
                case 2:
                    labelWait.Text = "...";
                    break;
                default:
                    labelWait.Text = "";
                    break;
            }
            timerTickCount++;
        }

        private void EditApplicationSettings_Load(object sender, EventArgs e)
        {
            y_spacing = Height - pictureBoxGameBoxCover.Bottom;
            UpdateDisplay(AppInfo);
            //Populate the game list
            if (autoSearchNewApp || gameList_UserChoice.Count == 0)
            {
                GetGameList();
            }
        }

        private void pictureBoxSearch_Click(object sender, EventArgs e)
        {
            GetGameList();
        }

        private void comboBoxGameList_SelectedIndexChanged(object sender, EventArgs e)
        {            
            UpdateDisplay((ApplicationInfo)comboBoxGameList.SelectedItem);
        }

        private void pictureBoxEditImage_Click(object sender, EventArgs e)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            string sep = string.Empty;

            openFileDialog1.InitialDirectory = Path.GetPathRoot(Application.ExecutablePath);
            openFileDialog1.Filter = "";
            foreach (var c in codecs)
            {
                string codecName = c.CodecName.Substring(8).Replace("Codec", "Files").Trim();
                openFileDialog1.Filter = String.Format("{0}{1}{2} ({3})|{3}", openFileDialog1.Filter, sep, codecName, c.FilenameExtension);
                sep = "|";
            }

            openFileDialog1.Filter = String.Format("{0}{1}{2} ({3})|{3}", openFileDialog1.Filter, sep, "All Files", "*.*");

            openFileDialog1.DefaultExt = ".Png"; // Default file extension 

            // Show open file dialog box 
            // Process open file dialog box results 
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Open document 
                    Image newImage = Image.FromFile(openFileDialog1.FileName);
                    if (CheckImageSize((Bitmap)newImage, 20971520))
                        pictureBoxGameBoxCover.Image = newImage;
                    else
                        MessageBox.Show("Image is to large for database. Limit is 20MB.", "Image Size Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                catch(OutOfMemoryException ex)
                {
                    MessageBox.Show("Image size to large");
                    logger.Info("Edit Application: {0}", ex.Message);
                }
            }
        }

        private void pictureBoxChangeFileLocation_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Executable (*.exe)|*.exe| All files (*.*)|*.*";
            openFileDialog1.FileName = "";
            openFileDialog1.InitialDirectory = Path.GetDirectoryName(textBoxAppPath.Text);
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBoxAppPath.Text = openFileDialog1.FileName;                
            }
        }

        private void pictureBoxDisabled_Click(object sender, EventArgs e)
        {
            AppInfo.Enabled = true;
            pictureBoxDisabled.Visible = false;
            pictureBoxEnabled.Visible = true;
        }

        private void pictureBoxEnabled_Click(object sender, EventArgs e)
        {
            AppInfo.Enabled = false;
            pictureBoxDisabled.Visible = true;
            pictureBoxEnabled.Visible = false;
        }

        private void pictureBoxApply_Click(object sender, EventArgs e)
        {
            //Copy values 
            AppInfo.Image = (Bitmap)pictureBoxGameBoxCover.Image;
            AppInfo.DisplayName = textBoxDisplayName.Text;
            AppInfo.FullPath = textBoxAppPath.Text;

            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void pictureBoxCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
