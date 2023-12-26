﻿using ForzaDSX.Config;
using ForzaDSX.Properties;
using System;
using System.ComponentModel;
using System.Linq;

//using System.Configuration;
using System.Threading;
using System.Windows.Forms;
using static ForzaDSX.ForzaDSXWorker;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ForzaDSX
{
    public partial class UI : Form
    {
        protected ForzaDSXWorker forzaDSXWorker;
        AppCheckThread appCheckWorker;
        protected ForzaDSX.Config.Config currentSettings;
        protected ForzaDSX.Config.Profile selectedProfile;
        BindingList<String> executables = new BindingList<string>();
        int selectedIndex = 0;
        //protected Configuration config;
        public ForzaDSX.Config.Config CurrentSettings { get => currentSettings; set => currentSettings = value; }

        bool bForzaConnected = false;
        bool bDsxConnected = false;

        Thread appCheckThread;
        Thread forzaDsxThread;

        CancellationTokenSource appCheckThreadCancellationToken;
        CancellationToken appCheckThreadToken;

        CancellationTokenSource forzaThreadCancellationToken;
        CancellationToken forzaThreadToken;

        public UI()
        {
            InitializeComponent();

            //forzaDSXWorker = new ForzaDSXWorker(this);
        }

        void UpdateDSXConnectionStatus(bool bConnected)
        {
            toolStripStatusDSX.Image = bConnected ? Resources.greenBtn : Resources.redBtn;
            bDsxConnected = bConnected;
        }

        void UpdateForzaConnectionStatus(bool bConnected)
        {
            toolStripStatusForza.Image = bConnected ? Resources.greenBtn : Resources.redBtn;
            bForzaConnected = bConnected;
        }

        public void Output(string Text, bool bShowMessageBox = false)
        {
            outputListBox.Items.Insert(0, Text);

            if (outputListBox.Items.Count > 50)
            {
                outputListBox.Items.RemoveAt(50);
            }

            if (bShowMessageBox)
            {
                MessageBox.Show(Text);
            }
        }

        private void UI_Load(object sender, EventArgs e)
        {
            this.Text = "ForzaDSX version: " + Program.VERSION;

            LoadSettings();

            noRaceText.Text = String.Empty;
            throttleVibrationMsg.Text = String.Empty;
            throttleMsg.Text = String.Empty;
            brakeVibrationMsg.Text = String.Empty;
            brakeMsg.Text = String.Empty;

            noRaceGroupBox.Visible = currentSettings.VerboseLevel > Config.VerboseLevel.Off;
            raceGroupBox.Visible = currentSettings.VerboseLevel > Config.VerboseLevel.Off;

            // Starts the background Worker
            //this.connectionWorker.RunWorkerAsync();





            var forzaProgressHandler = new Progress<ForzaDSXReportStruct>(WorkerThreadReporter);

            forzaDSXWorker = new ForzaDSXWorker(currentSettings, forzaProgressHandler);

            forzaThreadCancellationToken = new CancellationTokenSource();
            forzaThreadToken = forzaThreadCancellationToken.Token;

            forzaThreadToken.Register(() => forzaDSXWorker.Stop());
            var progressHandler = new Progress<AppCheckReportStruct>(AppCheckReporter);
            appCheckWorker = new AppCheckThread(ref currentSettings, progressHandler);
            appCheckThreadCancellationToken = new CancellationTokenSource();
            appCheckThreadToken = appCheckThreadCancellationToken.Token;

            appCheckThreadToken.Register(() => appCheckWorker.Stop());
            if (!currentSettings.DisableAppCheck)
            {
                startAppCheckThread();

            }
            else
            {
                UpdateDSXConnectionStatus(true);
                UpdateForzaConnectionStatus(true);
                StartForzaDSXThread();
            }
        }

        protected void startAppCheckThread()
        {


            appCheckThread = new Thread(new ThreadStart(appCheckWorker.Run));
            appCheckThread.IsBackground = true;

            appCheckThread.Start();
        }

        protected void AppCheckReporter(AppCheckReportStruct value)
        {
            if (value.type == AppCheckReportStruct.AppType.NONE)
            {
                Output(value.message);
            }
            else if (value.type == AppCheckReportStruct.AppType.DSX)
            {
                UpdateDSXConnectionStatus(value.value);
            }
            else
            {
                UpdateForzaConnectionStatus(value.value);
                if (value.value)
                {
                    SwitchActiveProfile(value.message);
                } else
                {
                    SwitchActiveProfile(null);
                }
            }

            if (forzaDsxThread == null)
            {
                if (bForzaConnected && bDsxConnected)
                {
                    StartForzaDSXThread();
                }
            }
            else
            {
                if (!bForzaConnected || !bDsxConnected)
                {
                    StopForzaDSXThread();
                }
            }
        }

        protected void SwitchActiveProfile(String profileName)
        {
            Profile profile = null;
           
            if (profileName == "")
            {
                //   profileName = selectedProfile.Name;
                return;
            }
            if (currentSettings.ActiveProfile != null && currentSettings.ActiveProfile.Name == profileName)
                return;

            if (profileName != null && currentSettings.Profiles.ContainsKey(profileName))
            {
                profile = currentSettings.Profiles[profileName];
               
            }
            currentSettings.ActiveProfile = profile;
            ConfigHandler.SaveConfig();
            loadProfilesIntoList();
            SwitchDisplayedProfile(profileName);
            StopForzaDSXThread();
            StartForzaDSXThread();

        }

        protected void RestartAppCheckThread()
        {
            StopAppCheckThread();
            startAppCheckThread();
        }

        protected void StartForzaDSXThread()
        {
            if (forzaDsxThread != null
                || forzaDSXWorker == null)
                return;
            if (currentSettings.ActiveProfile == null)
                return;
            forzaDsxThread = new Thread(new ThreadStart(forzaDSXWorker.Run));
            forzaDsxThread.IsBackground = true;

            forzaDsxThread.Start();
        }

        protected void StopForzaDSXThread()
        {
            try
            {
                if (forzaDsxThread != null
                    && forzaThreadCancellationToken != null)
                {
                    forzaThreadCancellationToken.Cancel();
                }
            }
            catch (Exception)
            {

                throw;
            }

            forzaDsxThread = null;
        }
        private void disableAppCheck()
        {
            currentSettings.DisableAppCheck = true;
            toolStripAppCheckOnItem.Checked = false;
            toolStripAppCheckOffItem.Checked = true;
            toolStripAppCheckButton.Text = "App Check Disabled";
            StopAppCheckThread();
            SwitchActiveProfile(currentSettings.DefaultProfile);
            UpdateDSXConnectionStatus(true);
            UpdateForzaConnectionStatus(true);
            StartForzaDSXThread();
            ConfigHandler.SaveConfig();
        }
        protected void StopAppCheckThread()
        {
            try
            {
                if (appCheckThread != null
                    && appCheckThreadCancellationToken != null)
                {
                    appCheckThreadCancellationToken.Cancel();
                }
            }
            catch (Exception)
            {

                throw;
            }

            appCheckThread = null;
        }

        protected void WorkerThreadReporter(ForzaDSXReportStruct value)
        {
            switch (value.type)
            {
                case ForzaDSXReportStruct.ReportType.VERBOSEMESSAGE:
                    Output(value.message);
                    break;
                case ForzaDSXReportStruct.ReportType.NORACE:
                    if (currentSettings.VerboseLevel > Config.VerboseLevel.Off)
                    {
                        noRaceGroupBox.Visible = true;
                        raceGroupBox.Visible = false;
                    }

                    noRaceText.Text = value.message;
                    break;
                case ForzaDSXReportStruct.ReportType.RACING:
                    if (currentSettings.VerboseLevel > Config.VerboseLevel.Off)
                    {
                        noRaceGroupBox.Visible = false;
                        raceGroupBox.Visible = true;
                    }

                    switch (value.racingType)
                    {
                        case ForzaDSXReportStruct.RacingReportType.THROTTLE_VIBRATION:
                            throttleVibrationMsg.Text = value.message;
                            break;
                        case ForzaDSXReportStruct.RacingReportType.THROTTLE:
                            throttleMsg.Text = value.message;
                            break;
                        case ForzaDSXReportStruct.RacingReportType.BRAKE_VIBRATION:
                            brakeVibrationMsg.Text = value.message;
                            break;
                        case ForzaDSXReportStruct.RacingReportType.BRAKE:
                            brakeMsg.Text = value.message;
                            break;
                    }
                    break;
            }
        }

        private void UI_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            appCheckThreadCancellationToken.Cancel();
            appCheckThreadCancellationToken.Dispose();

            forzaThreadCancellationToken.Cancel();
            forzaThreadCancellationToken.Dispose();
        }



        public void LoadSettings()
        {
            //// Build a config object, using env vars and JSON providers.
            //config = new ConfigurationBuilder()
            //	.AddIniFile("appsettings.ini")
            //	.Build();

            try
            {

                // Get values from the config given their key and their target type.
                currentSettings = ConfigHandler.GetConfig();
                selectedProfile = currentSettings.Profiles.Values.First();

                if (currentSettings.DisableAppCheck && currentSettings.DefaultProfile != null)
                {
                    if (currentSettings.Profiles.ContainsKey(currentSettings.DefaultProfile))
                    {
                        currentSettings.ActiveProfile = currentSettings.Profiles[currentSettings.DefaultProfile];
                    }
                }

                verboseModeOffToolStripMenuItem.Checked = currentSettings.VerboseLevel == VerboseLevel.Off;
                verboseModeLowToolStripMenuItem.Checked = currentSettings.VerboseLevel == VerboseLevel.Limited;
                verboseModeFullToolStripMenuItem.Checked = currentSettings.VerboseLevel == VerboseLevel.Full;
                toolStripDSXPortButton.Text = "DSX Port: " + currentSettings.DSXPort.ToString();
                toolStripVerboseMode.Text = "Verbose Mode: " + currentSettings.VerboseLevel.ToString();
            }
            catch (Exception e)
            {
                Output("Invalid Configuration File!\n" + e.Message, true);
            }

            SetupUI();
        }

        #region UI Forms control
        void SetupUI()
        {
            // Misc panel


            if (currentSettings.DisableAppCheck)
            {
                toolStripAppCheckOnItem.Checked = false;
                toolStripAppCheckOffItem.Checked = true;
                toolStripAppCheckButton.Text = "App Check Disabled";
            }
            else
            {
                toolStripAppCheckOnItem.Checked = true;
                toolStripAppCheckOffItem.Checked = false;
                toolStripAppCheckButton.Text = "App Check Enabled";
            }

            toolStripDSXPortButton.Text = "DSX Port: " + currentSettings.DSXPort.ToString();
            toolStripDSXPortTextBox.Text = currentSettings.DSXPort.ToString();


            loadProfilesIntoList();
            SwitchDisplayedProfile();

        }

        void loadProfilesIntoList()
        {
            profilesListView.Items.Clear();
            //Load Profiles into list
            foreach (Profile profile in currentSettings.Profiles.Values)
            {
                String name = profile.Name;
                ListViewItem item = new ListViewItem(name);

                if (!profile.IsEnabled)
                {
                    name += " (Disabled)";
                }
                if (profile == currentSettings.ActiveProfile)
                {
                    name += " (Active)";
                    item.Selected = true;
                }
                if (profile.Name == currentSettings.DefaultProfile)
                {
                    name += " (Default)";
                }
                item.Text = name;
                item.Name = profile.Name;
                profilesListView.Items.Add(item);
            }
        }

        void SwitchDisplayedProfile(String profileName = "")
        {
            
            if (profileName == null || profileName == "" )
            {
                if (selectedProfile == null)
                {
                    selectedProfile = currentSettings.Profiles.Values.First();
                }
                profileName = selectedProfile.Name;
            }
            if (currentSettings.Profiles.ContainsKey(profileName))
            {
                selectedProfile = currentSettings.Profiles[profileName];
            }
            executables = new BindingList<string>(selectedProfile.executableNames);
            ExecutableListBox.DataSource = executables;


            BrakeSettings brakeSettings = selectedProfile.brakeSettings;
            ThrottleSettings throttleSettings = selectedProfile.throttleSettings;

            brakeSettings.EffectIntensity = Math.Clamp(brakeSettings.EffectIntensity, 0.0f, 1.0f);
            throttleSettings.EffectIntensity = Math.Clamp(throttleSettings.EffectIntensity, 0.0f, 1.0f);
            this.rpmTrackBar.Value = DenormalizeValue(selectedProfile.RPMRedlineRatio);
            rpmValueNumericUpDown.Value = rpmTrackBar.Value;
            this.forzaPortNumericUpDown.Value = selectedProfile.gameUDPPort;

            // Brake Panel
            this.brakeTriggerModeComboBox.SelectedIndex = (int)brakeSettings.TriggerMode;
            this.brakeEffectIntensityTrackBar.Value = DenormalizeValue(brakeSettings.EffectIntensity);
            this.gripLossTrackBar.Value = DenormalizeValue(brakeSettings.GripLossValue);
            this.brakeVibrationStartTrackBar.Value = brakeSettings.VibrationStart;
            this.brakeVibrationModeTrackBar.Value = brakeSettings.VibrationModeStart;
            this.minBrakeVibrationTrackBar.Value = brakeSettings.MinVibration;
            this.maxBrakeVibrationTrackBar.Value = brakeSettings.MaxVibration;
            this.vibrationSmoothingTrackBar.Value = DenormalizeValue(brakeSettings.VibrationSmoothing, 500.0f);
            this.minBrakeStiffnessTrackBar.Value = brakeSettings.MinStiffness;
            this.maxBrakeStiffnessTrackBar.Value = brakeSettings.MaxStiffness;
            this.minBrakeResistanceTrackBar.Value = brakeSettings.MinResistance;
            this.maxBrakeResistanceTrackBar.Value = brakeSettings.MaxResistance;
            this.brakeResistanceSmoothingTrackBar.Value = DenormalizeValue(brakeSettings.ResistanceSmoothing, 500.0f);

            this.brakeEffectNumericUpDown.Value = this.brakeEffectIntensityTrackBar.Value;
            this.gripLossNumericUpDown.Value = this.gripLossTrackBar.Value;
            this.brakeVibrationStartNumericUpDown.Value = this.brakeVibrationStartTrackBar.Value;
            this.brakeVibrationModeNumericUpDown.Value = this.brakeVibrationModeTrackBar.Value;
            this.minBrakeVibrationNumericUpDown.Value = this.minBrakeVibrationTrackBar.Value;
            this.maxBrakeVibrationNumericUpDown.Value = this.maxBrakeVibrationTrackBar.Value;
            this.brakeVibrationSmoothNumericUpDown.Value = this.vibrationSmoothingTrackBar.Value;
            this.minBrakeStifnessNumericUpDown.Value = this.minBrakeStiffnessTrackBar.Value;
            this.maxBrakeStifnessNumericUpDown.Value = this.maxBrakeStiffnessTrackBar.Value;
            this.minBrakeResistanceNumericUpDown.Value = this.minBrakeResistanceTrackBar.Value;
            this.maxBrakeResistanceNumericUpDown.Value = this.maxBrakeResistanceTrackBar.Value;
            this.brakeResistanceSmoothNumericUpDown.Value = this.brakeResistanceSmoothingTrackBar.Value;

            // Throttle Panel
            this.throttleTriggerModeComboBox.SelectedIndex = (int)throttleSettings.TriggerMode;
            this.throttleIntensityTrackBar.Value = DenormalizeValue(throttleSettings.EffectIntensity);
            this.throttleGripLossTrackBar.Value = DenormalizeValue(throttleSettings.GripLossValue);
            this.throttleTurnAccelScaleTrackBar.Value = DenormalizeValue(throttleSettings.TurnAccelerationScale);
            this.throttleForwardAccelScaleTrackBar.Value = DenormalizeValue(throttleSettings.ForwardAccelerationScale);
            this.throttleAccelLimitTrackBar.Value = throttleSettings.AccelerationLimit;
            this.throttleVibrationModeStartTrackBar.Value = throttleSettings.VibrationModeStart;
            this.throttleMinVibrationTrackBar.Value = throttleSettings.MinVibration;
            this.throttleMaxVibrationTrackBar.Value = throttleSettings.MaxVibration;
            this.throttleVibrationSmoothTrackBar.Value = DenormalizeValue(throttleSettings.VibrationSmoothing);
            this.throttleMinStiffnessTrackBar.Value = throttleSettings.MinStiffness;
            this.throttleMaxStiffnessTrackBar.Value = throttleSettings.MaxStiffness;
            this.throttleMinResistanceTrackBar.Value = throttleSettings.MinResistance;
            this.throttleMaxResistanceTrackBar.Value = throttleSettings.MaxResistance;
            this.throttleResistanceSmoothTrackBar.Value = DenormalizeValue(throttleSettings.ResistanceSmoothing);

            this.throttleIntensityNumericUpDown.Value = this.throttleIntensityTrackBar.Value;
            this.throttleGripLossNumericUpDown.Value = this.throttleGripLossTrackBar.Value;
            this.throttleTurnAccelScaleNumericUpDown.Value = this.throttleTurnAccelScaleTrackBar.Value;
            this.throttleForwardAccelScaleNumericUpDown.Value = this.throttleForwardAccelScaleTrackBar.Value;
            this.throttleAccelLimitNumericUpDown.Value = this.throttleAccelLimitTrackBar.Value;
            this.throttleVibrationStartNumericUpDown.Value = this.throttleVibrationModeStartTrackBar.Value;
            this.throttleMinVibrationNumericUpDown.Value = this.throttleMinVibrationTrackBar.Value;
            this.throttleMaxVibrationNumericUpDown.Value = this.throttleMaxVibrationTrackBar.Value;
            this.throttleVibrationSmoothNumericUpDown.Value = this.throttleVibrationSmoothTrackBar.Value;
            this.throttleMinStiffnessNumericUpDown.Value = this.throttleMinStiffnessTrackBar.Value;
            this.throttleMaxStiffnessNumericUpDown.Value = this.throttleMaxStiffnessTrackBar.Value;
            this.throttleMinResistanceNumericUpDown.Value = this.throttleMinResistanceTrackBar.Value;
            this.throttleMaxResistanceNumericUpDown.Value = this.throttleMaxResistanceTrackBar.Value;
            this.throttleResistanceSmoothNumericUpDown.Value = this.throttleResistanceSmoothTrackBar.Value;
        }

        static int DenormalizeValue(float normalizedValue, float scale = 100.0f)
        {
            return (int)Math.Floor(normalizedValue * scale);
        }

        static float NormalizeValue(float value, float scale = 100.0f)
        {
            if (scale == 0)
                return value;

            return value / scale;
        }

        private void verboseModeFullToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentSettings.VerboseLevel = VerboseLevel.Full;
            verboseModeOffToolStripMenuItem.Checked = false;
            verboseModeLowToolStripMenuItem.Checked = false;
            verboseModeFullToolStripMenuItem.Checked = true;
            toolStripVerboseMode.Text = "Verbose Mode: " + currentSettings.VerboseLevel.ToString();
            ConfigHandler.SaveConfig();

        }

        private void verboseModeLowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentSettings.VerboseLevel = VerboseLevel.Limited;
            verboseModeOffToolStripMenuItem.Checked = false;
            verboseModeLowToolStripMenuItem.Checked = true;
            verboseModeFullToolStripMenuItem.Checked = false;
            toolStripVerboseMode.Text = "Verbose Mode: " + currentSettings.VerboseLevel.ToString();
            ConfigHandler.SaveConfig();


        }

        private void verboseModeOffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            currentSettings.VerboseLevel = VerboseLevel.Off;
            verboseModeOffToolStripMenuItem.Checked = true;
            verboseModeLowToolStripMenuItem.Checked = false;
            verboseModeFullToolStripMenuItem.Checked = false;
            toolStripVerboseMode.Text = "Verbose Mode: " + currentSettings.VerboseLevel.ToString();

            noRaceGroupBox.Visible = false;
            raceGroupBox.Visible = false;
            ConfigHandler.SaveConfig();

        }

        #region Misc





        private void rpmTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.RPMRedlineRatio = NormalizeValue(this.rpmTrackBar.Value);
            rpmValueNumericUpDown.Value = rpmTrackBar.Value;


        }

        private void rpmValueNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.RPMRedlineRatio = NormalizeValue((float)this.rpmValueNumericUpDown.Value);
            rpmTrackBar.Value = (int)Math.Floor(rpmValueNumericUpDown.Value);


        }

        private void forzaPortNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.gameUDPPort = (int)Math.Floor(this.forzaPortNumericUpDown.Value);


        }
        #endregion

        #region Brake
        private void brakeEffectIntensityTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.EffectIntensity = NormalizeValue(brakeEffectIntensityTrackBar.Value);
            this.brakeEffectNumericUpDown.Value = brakeEffectIntensityTrackBar.Value;


        }

        private void brakeEffectNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.EffectIntensity = NormalizeValue((float)brakeEffectNumericUpDown.Value);
            brakeEffectIntensityTrackBar.Value = (int)Math.Floor(brakeEffectNumericUpDown.Value);


        }

        private void gripLossTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.GripLossValue = NormalizeValue(gripLossTrackBar.Value);
            gripLossNumericUpDown.Value = gripLossTrackBar.Value;


        }

        private void gripLossNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.GripLossValue = NormalizeValue((float)gripLossNumericUpDown.Value);
            gripLossTrackBar.Value = (int)Math.Floor(gripLossNumericUpDown.Value);


        }

        private void brakeVibrationStartTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.VibrationStart = brakeVibrationStartTrackBar.Value;
            brakeVibrationStartNumericUpDown.Value = brakeVibrationStartTrackBar.Value;


        }

        private void brakeVibrationStartNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.VibrationStart = (int)Math.Floor(brakeVibrationStartNumericUpDown.Value);
            brakeVibrationStartTrackBar.Value = selectedProfile.brakeSettings.VibrationStart;


        }

        private void brakeVibrationModeTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.VibrationModeStart = brakeVibrationModeTrackBar.Value;
            brakeVibrationModeNumericUpDown.Value = brakeVibrationModeTrackBar.Value;


        }

        private void brakeVibrationModeNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.VibrationModeStart = (int)Math.Floor(brakeVibrationModeNumericUpDown.Value);
            brakeVibrationModeTrackBar.Value = selectedProfile.brakeSettings.VibrationModeStart;


        }

        private void minBrakeVibrationTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MinVibration = minBrakeVibrationTrackBar.Value;
            minBrakeVibrationNumericUpDown.Value = minBrakeVibrationTrackBar.Value;


        }

        private void minBrakeVibrationNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MinVibration = (int)Math.Floor(minBrakeVibrationNumericUpDown.Value);
            minBrakeVibrationTrackBar.Value = selectedProfile.brakeSettings.MinVibration;


        }

        private void maxBrakeVibrationTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MaxVibration = maxBrakeVibrationTrackBar.Value;
            maxBrakeVibrationNumericUpDown.Value = maxBrakeVibrationTrackBar.Value;


        }

        private void maxBrakeVibrationNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MaxVibration = (int)Math.Floor(maxBrakeVibrationNumericUpDown.Value);
            maxBrakeVibrationTrackBar.Value = selectedProfile.brakeSettings.MaxVibration;


        }

        private void vibrationSmoothingTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.VibrationSmoothing = NormalizeValue(vibrationSmoothingTrackBar.Value, 500);
            brakeVibrationSmoothNumericUpDown.Value = vibrationSmoothingTrackBar.Value;


        }

        private void brakeVibrationSmoothNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.VibrationSmoothing = NormalizeValue((float)brakeVibrationSmoothNumericUpDown.Value, 500);
            vibrationSmoothingTrackBar.Value = (int)Math.Floor(brakeVibrationSmoothNumericUpDown.Value);


        }

        private void minBrakeStiffnessTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MinStiffness = minBrakeStiffnessTrackBar.Value;
            minBrakeStifnessNumericUpDown.Value = minBrakeStiffnessTrackBar.Value;


        }

        private void minBrakeStifnessNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MinStiffness = (int)Math.Floor(minBrakeVibrationNumericUpDown.Value);
            minBrakeVibrationTrackBar.Value = selectedProfile.brakeSettings.MinStiffness;


        }

        private void maxBrakeStiffnessTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MaxStiffness = maxBrakeStiffnessTrackBar.Value;
            maxBrakeStifnessNumericUpDown.Value = maxBrakeStiffnessTrackBar.Value;


        }

        private void maxBrakeStifnessNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.MaxStiffness = (int)Math.Floor(maxBrakeVibrationNumericUpDown.Value);
            maxBrakeVibrationTrackBar.Value = selectedProfile.brakeSettings.MaxStiffness;


        }

        private void minBrakeResistanceTrackBar_Scroll(object sender, EventArgs e)
        {

            int value = minBrakeResistanceTrackBar.Value;
            if (value > selectedProfile.brakeSettings.MaxResistance)
                value = selectedProfile.brakeSettings.MaxResistance;

            selectedProfile.brakeSettings.MinResistance = value;

            minBrakeResistanceTrackBar.Value = value;
            minBrakeResistanceNumericUpDown.Value = value;


        }

        private void minBrakeResistanceNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(minBrakeResistanceNumericUpDown.Value);
            if (value > selectedProfile.brakeSettings.MaxResistance)
                value = selectedProfile.brakeSettings.MaxResistance;

            selectedProfile.brakeSettings.MinResistance = value;

            minBrakeResistanceTrackBar.Value = value;
            minBrakeResistanceNumericUpDown.Value = value;


        }

        private void maxBrakeResistanceTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = maxBrakeResistanceTrackBar.Value;

            if (value > selectedProfile.brakeSettings.MinResistance)
                value = selectedProfile.brakeSettings.MinResistance;

            selectedProfile.brakeSettings.MaxResistance = value;
            maxBrakeResistanceTrackBar.Value = value;
            maxBrakeResistanceNumericUpDown.Value = value;


        }

        private void maxBrakeResistanceNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(maxBrakeResistanceNumericUpDown.Value);
            if (value > selectedProfile.brakeSettings.MinResistance)
                value = selectedProfile.brakeSettings.MinResistance;

            selectedProfile.brakeSettings.MaxResistance = value;

            maxBrakeResistanceTrackBar.Value = value;
            maxBrakeResistanceNumericUpDown.Value = value;


        }

        private void brakeResistanceSmoothingTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.ResistanceSmoothing = NormalizeValue(brakeResistanceSmoothingTrackBar.Value, 500);
            brakeResistanceSmoothNumericUpDown.Value = brakeResistanceSmoothingTrackBar.Value;


        }

        private void brakeResistanceSmoothNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.ResistanceSmoothing = NormalizeValue((float)brakeResistanceSmoothNumericUpDown.Value, 500);
            brakeResistanceSmoothingTrackBar.Value = (int)Math.Floor(brakeResistanceSmoothNumericUpDown.Value);


        }
        #endregion

        #region Throttle
        private void throttleIntensityTrackBar_Scroll(object sender, EventArgs e)
        {
            selectedProfile.throttleSettings.EffectIntensity = NormalizeValue(throttleIntensityTrackBar.Value);
            throttleIntensityNumericUpDown.Value = throttleIntensityTrackBar.Value;


        }

        private void throttleIntensityNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            selectedProfile.throttleSettings.EffectIntensity = NormalizeValue((float)throttleIntensityNumericUpDown.Value);
            throttleIntensityTrackBar.Value = (int)Math.Floor(throttleIntensityNumericUpDown.Value);


        }

        private void throttleGripLossTrackBar_Scroll(object sender, EventArgs e)
        {

            int value = throttleGripLossTrackBar.Value;
            selectedProfile.throttleSettings.GripLossValue = NormalizeValue(value);
            throttleGripLossNumericUpDown.Value = value;


        }

        private void throttleGripLossNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            float value = (float)throttleGripLossNumericUpDown.Value;
            selectedProfile.throttleSettings.GripLossValue = NormalizeValue(value);
            throttleGripLossTrackBar.Value = (int)Math.Floor(value);


        }

        private void throttleTurnAccelScaleTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleTurnAccelScaleTrackBar.Value;
            selectedProfile.throttleSettings.TurnAccelerationScale = NormalizeValue(value);
            throttleTurnAccelScaleNumericUpDown.Value = value;


        }

        private void throttleTurnAccelScaleNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            float value = (float)throttleTurnAccelScaleNumericUpDown.Value;
            selectedProfile.throttleSettings.TurnAccelerationScale = NormalizeValue(value);
            throttleTurnAccelScaleTrackBar.Value = (int)Math.Floor(value);


        }

        private void throttleForwardAccelScaleTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleForwardAccelScaleTrackBar.Value;
            selectedProfile.throttleSettings.ForwardAccelerationScale = NormalizeValue(value);
            throttleForwardAccelScaleNumericUpDown.Value = value;


        }

        private void throttleForwardAccelScaleNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            float value = (float)throttleForwardAccelScaleNumericUpDown.Value;
            selectedProfile.throttleSettings.ForwardAccelerationScale = NormalizeValue(value);
            throttleForwardAccelScaleTrackBar.Value = (int)Math.Floor(value);


        }

        private void throttleAccelLimitTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleAccelLimitTrackBar.Value;
            selectedProfile.throttleSettings.AccelerationLimit = value;
            throttleAccelLimitNumericUpDown.Value = value;


        }

        private void throttleAccelLimitNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleAccelLimitNumericUpDown.Value);
            selectedProfile.throttleSettings.AccelerationLimit = value;
            throttleAccelLimitTrackBar.Value = value;


        }

        private void throttleVibrationModeStartTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleVibrationModeStartTrackBar.Value;
            selectedProfile.throttleSettings.VibrationModeStart = value;
            throttleVibrationStartNumericUpDown.Value = value;


        }

        private void throttleVibrationStartNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleVibrationStartNumericUpDown.Value);
            selectedProfile.throttleSettings.VibrationModeStart = value;
            throttleVibrationModeStartTrackBar.Value = value;


        }

        private void throttleMinVibrationTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleMinVibrationTrackBar.Value;
            selectedProfile.throttleSettings.MinVibration = value;
            throttleMinVibrationNumericUpDown.Value = value;


        }

        private void throttleMinVibrationNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleMinVibrationNumericUpDown.Value);
            selectedProfile.throttleSettings.MinVibration = value;
            throttleMinVibrationTrackBar.Value = value;


        }

        private void throttleMaxVibrationTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleMaxVibrationTrackBar.Value;
            selectedProfile.throttleSettings.MaxVibration = value;
            throttleMaxVibrationNumericUpDown.Value = value;


        }

        private void throttleMaxVibrationNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleMaxVibrationNumericUpDown.Value);
            selectedProfile.throttleSettings.MaxVibration = value;
            throttleMaxVibrationTrackBar.Value = value;


        }

        private void throttleVibrationSmoothTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleVibrationSmoothTrackBar.Value;
            selectedProfile.throttleSettings.VibrationSmoothing = NormalizeValue(value);
            throttleVibrationSmoothNumericUpDown.Value = value;


        }

        private void throttleVibrationSmoothNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            float value = (float)throttleVibrationSmoothNumericUpDown.Value;
            selectedProfile.throttleSettings.VibrationSmoothing = NormalizeValue(value);
            throttleVibrationSmoothTrackBar.Value = (int)Math.Floor(value);


        }

        private void throttleMinStiffnessTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleMinStiffnessTrackBar.Value;
            selectedProfile.throttleSettings.MinStiffness = value;
            throttleMinStiffnessNumericUpDown.Value = value;


        }

        private void throttleMinStiffnessNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleMinStiffnessNumericUpDown.Value);
            selectedProfile.throttleSettings.MinStiffness = value;
            throttleMinStiffnessTrackBar.Value = value;


        }

        private void throttleMaxStiffnessTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleMaxStiffnessTrackBar.Value;
            selectedProfile.throttleSettings.MaxStiffness = value;
            throttleMaxStiffnessNumericUpDown.Value = value;


        }

        private void throttleMaxStiffnessNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleMaxStiffnessNumericUpDown.Value);
            selectedProfile.throttleSettings.MaxStiffness = value;
            throttleMaxStiffnessTrackBar.Value = value;


        }

        private void throttleMinResistanceTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleMinResistanceTrackBar.Value;
            selectedProfile.throttleSettings.MinResistance = value;
            throttleMinResistanceNumericUpDown.Value = value;


        }

        private void throttleMinResistanceNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleMinResistanceNumericUpDown.Value);
            selectedProfile.throttleSettings.MinResistance = value;
            throttleMinResistanceTrackBar.Value = value;


        }

        private void throttleMaxResistanceTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleMaxResistanceTrackBar.Value;
            selectedProfile.throttleSettings.MaxResistance = value;
            throttleMaxResistanceNumericUpDown.Value = value;


        }

        private void throttleMaxResistanceNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int value = (int)Math.Floor(throttleMaxResistanceNumericUpDown.Value);
            selectedProfile.throttleSettings.MaxResistance = value;
            throttleMaxResistanceTrackBar.Value = value;


        }

        private void throttleResistanceSmoothTrackBar_Scroll(object sender, EventArgs e)
        {
            int value = throttleResistanceSmoothTrackBar.Value;
            selectedProfile.throttleSettings.ResistanceSmoothing = NormalizeValue(value);
            throttleResistanceSmoothNumericUpDown.Value = value;


        }

        private void throttleResistanceSmoothNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            float value = (float)throttleResistanceSmoothNumericUpDown.Value;
            selectedProfile.throttleSettings.ResistanceSmoothing = NormalizeValue(value);
            throttleResistanceSmoothTrackBar.Value = (int)Math.Floor(value);


        }
        #endregion

        #endregion

        private void buttonApplyMisc_Click(object sender, EventArgs e)
        {
            if (forzaDSXWorker != null)
            {
                selectedProfile.executableNames = executables.ToList();
                forzaDSXWorker.SetSettings(CurrentSettings);
                ConfigHandler.SaveConfig();
                appCheckWorker.updateExecutables();
                //RestartAppCheckThread();

            }
        }

        private void buttonApply_Brake_Click(object sender, EventArgs e)
        {
            if (forzaDSXWorker != null)
            {

                forzaDSXWorker.SetSettings(CurrentSettings);
                ConfigHandler.SaveConfig();

            }
        }

        private void buttonApply_Throttle_Click(object sender, EventArgs e)
        {
            if (forzaDSXWorker != null)
            {

                forzaDSXWorker.SetSettings(CurrentSettings);
                ConfigHandler.SaveConfig();

            }
        }

        private void miscDefaultsButton_Click(object sender, EventArgs e)
        {

            selectedProfile.RPMRedlineRatio = 0.9f;
            selectedProfile.gameUDPPort = 9999;
            FullResetValues();
        }

        private void brakeDefaultsButton_Click(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings = new BrakeSettings();
            FullResetValues();
        }

        private void throttleDefaultsButton_Click(object sender, EventArgs e)
        {
            selectedProfile.throttleSettings = new ThrottleSettings();
            FullResetValues();
        }

        protected void FullResetValues()
        {
            // CurrentSettings.Reset();

            SetupUI();

            if (forzaDSXWorker != null)
            {
                // CurrentSettings.Save();
                ConfigHandler.SaveConfig();
                forzaDSXWorker.SetSettings(CurrentSettings);

                StartForzaDSXThread();
            }
        }

        private void brakeTriggerModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedProfile.brakeSettings.TriggerMode = (Config.TriggerMode)(sbyte)brakeTriggerModeComboBox.SelectedIndex;
        }

        private void throttleTriggerModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedProfile.throttleSettings.TriggerMode = (Config.TriggerMode)(sbyte)throttleTriggerModeComboBox.SelectedIndex;
        }



        private void toolStripAppCheckOnItem_Click(object sender, EventArgs e)
        {
            currentSettings.DisableAppCheck = false;
            toolStripAppCheckOnItem.Checked = true;
            toolStripAppCheckOffItem.Checked = false;
            toolStripAppCheckButton.Text = "App Check Enabled";
            ConfigHandler.SaveConfig();
            RestartAppCheckThread();
        }






        private void toolStripAppCheckOffItem_Click(object sender, EventArgs e)
        {
           disableAppCheck();

        }

        private void toolStripDSXPortButton_Click(object sender, EventArgs e)
        {
            try
            {
                currentSettings.DSXPort = Int32.Parse(toolStripDSXPortTextBox.Text);
                ConfigHandler.SaveConfig();

            }
            catch (Exception)
            {
                toolStripDSXPortTextBox.Text = currentSettings.DSXPort.ToString();
            }
            toolStripDSXPortButton.Text = "DSX Port: " + currentSettings.DSXPort.ToString();
        }

        private void toolStripDSXPortTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)Keys.Enter)
            {
                try
                {
                    currentSettings.DSXPort = Int32.Parse(toolStripDSXPortTextBox.Text);
                    ConfigHandler.SaveConfig();
                }
                catch (Exception)
                {
                    toolStripDSXPortTextBox.Text = currentSettings.DSXPort.ToString();
                }
                toolStripDSXPortButton.Text = "DSX Port: " + currentSettings.DSXPort.ToString();
            }
        }

        private void profilesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (profilesListView.SelectedItems.Count == 0)
            {
                profilesListView.Items[selectedIndex].Selected = true;
                return;

            }

        }



        private void profilesListView_MouseDown(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo HI = profilesListView.HitTest(e.Location);


            if (e.Button == MouseButtons.Right)
            {
                if (HI.Item != null)
                {
                    profilesListView.FocusedItem = HI.Item;
                    newToolStripMenuItem.Enabled = true;
                    renameToolStripMenuItem.Enabled = true;
                    disableToolStripMenuItem.Enabled = true;
                    deleteToolStripMenuItem.Enabled = true;
                    copyToolStripMenuItem.Enabled = false;
                    defaultToolStripMenuItem.Enabled = true;
                    setActiveToolStripMenuItem.Enabled = true;


                    if (currentSettings.Profiles[HI.Item.Name].IsEnabled)
                    {
                        disableToolStripMenuItem.Text = "Disable";
                    }
                    else
                    {
                        disableToolStripMenuItem.Text = "Enable";
                    }
                    if (currentSettings.Profiles[HI.Item.Name] == currentSettings.ActiveProfile)
                    {
                        setActiveToolStripMenuItem.CheckState = CheckState.Checked;
                    }
                    else
                    {
                        setActiveToolStripMenuItem.CheckState = CheckState.Unchecked;
                    }
                    if (HI.Item.Name == currentSettings.DefaultProfile)
                    {
                        defaultToolStripMenuItem.CheckState = CheckState.Checked;
                    }
                    else
                    {
                        defaultToolStripMenuItem.CheckState = CheckState.Unchecked;
                    }
                    ProfilesContextMenu.Show(Cursor.Position);
                }
                else
                {
                    ProfilesContextMenu.Items[1].Enabled = false;
                    ProfilesContextMenu.Items[2].Enabled = false;
                    ProfilesContextMenu.Items[3].Enabled = false;
                    ProfilesContextMenu.Items[4].Enabled = false;
                    ProfilesContextMenu.Items[5].Enabled = false;
                    ProfilesContextMenu.Items[6].Enabled = false;
                    ProfilesContextMenu.Show(Cursor.Position);

                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (HI.Item == null)
                {
                    return;
                }
                selectedIndex = HI.Item.Index;
                String profileName = HI.Item.Name;
                // HI.Item.Selected = true;
                SwitchDisplayedProfile(profileName);
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String newProfileName = NameForm.ShowDialog("", "Please enter the Profile Name");
            if (newProfileName != "")
            {
                Profile newProfile = new Profile();
                newProfile.Name = newProfileName;
                currentSettings.Profiles.Add(newProfileName, newProfile);
                ConfigHandler.SaveConfig();
                loadProfilesIntoList();
            }

        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String oldProfileName = profilesListView.FocusedItem.Name;
            String newProfileName = NameForm.ShowDialog(oldProfileName, "Please enter the Profile Name");
            if (newProfileName != "" && oldProfileName != newProfileName)
            {
                Profile newProfile = currentSettings.Profiles[oldProfileName];
                currentSettings.Profiles.Remove(oldProfileName);
                newProfile.Name = newProfileName;
                currentSettings.Profiles.Add(newProfileName, newProfile);
                ConfigHandler.SaveConfig();
                loadProfilesIntoList();
            }

        }

        private void disableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String profileName = profilesListView.FocusedItem.Name;
            if (currentSettings.Profiles.ContainsKey(profileName))
            {
                Profile profile = currentSettings.Profiles[profileName];
                profile.IsEnabled = !profile.IsEnabled;
                //profile.IsEnabled = false;
                ConfigHandler.SaveConfig();
                loadProfilesIntoList();
                appCheckWorker.updateExecutables();
            }

        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String profileName = profilesListView.FocusedItem.Name;
            if (currentSettings.Profiles.ContainsKey(profileName))
            {
                currentSettings.Profiles.Remove(profileName);
                ConfigHandler.SaveConfig();
                loadProfilesIntoList();
            }

        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void defaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String profileName = profilesListView.FocusedItem.Name;
            if (currentSettings.Profiles.ContainsKey(profileName))
            {
                currentSettings.DefaultProfile = profileName;
                ConfigHandler.SaveConfig();
                loadProfilesIntoList();
            }
        }

        private void setActiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String profileName = profilesListView.FocusedItem.Name;
            if (currentSettings.Profiles.ContainsKey(profileName))
            {
                //currentSettings.ActiveProfile = currentSettings.Profiles[profileName];
                disableAppCheck();
                SwitchActiveProfile(profileName);

            }
        }

        private void AddExecutableButton_Click(object sender, EventArgs e)
        {
            String newExecutableName = NameForm.ShowDialog("", "Please enter the Executable Name"); ;
            if (newExecutableName != "")
            {
                executables.Add(newExecutableName);
                // ExecutableListBox.Items.Add(newExecutableName);

            }
        }

        private void EditExecutableButton_Click(object sender, EventArgs e)
        {
            String oldExecutableName = ExecutableListBox.SelectedItems[0].ToString();
            String newExecutableName = NameForm.ShowDialog(oldExecutableName, "Please enter the Executable Name"); ;
            if (newExecutableName != "")
            {
                int index = selectedProfile.executableNames.IndexOf(oldExecutableName);
                executables[index] = newExecutableName;
               // ExecutableListBox.SelectedIndex = -1;
                // ExecutableListBox.Items.Add(newExecutableName);

            }
        }

        private void RemoveExecutableButton_Click(object sender, EventArgs e)
        {
            String oldExecutableName = ExecutableListBox.SelectedItems[0].ToString();
            
                executables.Remove(oldExecutableName);

        }

        private void ExecutableListBox_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (ExecutableListBox.SelectedItems.Count > 1)
            {
                EditExecutableButton.Enabled = false;
                RemoveExecutableButton.Enabled = true;
                return;

            } else if (ExecutableListBox.SelectedItems.Count == 0)
            {
                EditExecutableButton.Enabled = false;
                RemoveExecutableButton.Enabled = false;
            } else if (ExecutableListBox.SelectedItems.Count == 1)
            {
                EditExecutableButton.Enabled = true;
                RemoveExecutableButton.Enabled = true;
            }
        }
    }
}