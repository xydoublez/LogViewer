﻿using BrightIdeasSoftware;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using woanware;
using System.Threading;
using System.Text;

namespace LogViewer
{
    /// <summary>
    /// 
    /// </summary>
    public partial class FormMain : Form
    {
        #region Member Variables
        private LogFile lf;
        private Searches searches;
        private HourGlass hourGlass;
        private readonly SynchronizationContext synchronizationContext;
        private CancellationTokenSource cancellationTokenSource;
        private List<ushort> filterIds;
        private bool processing;
        private Color highlightColour = Color.Lime;
        private Color contextColour = Color.LightGray;
        private Configuration config;
        private Global.ViewMode viewMode = Global.ViewMode.Standard;
        #endregion

        #region Constructor
        /// <summary>
        /// 
        /// </summary>
        public FormMain()
        {
            InitializeComponent();

            synchronizationContext = SynchronizationContext.Current;
            dropdownSearchType.SelectedIndex = 0;
            searches = new Searches();
            filterIds = new List<ushort>();
        }
        #endregion

        #region Form Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_Load(object sender, EventArgs e)
        {
            this.config = new Configuration();
            string ret = this.config.Load();
            if (ret.Length > 0)
            {
                UserInterface.DisplayErrorMessageBox(this, ret);
            }

            this.highlightColour = config.GetHighlightColour();
            this.contextColour = config.GetContextColour();

            this.olvcLineNumber.AspectGetter = delegate (object x)
            {
                if (((LogLine)x) == null )
                {
                    return "";
                }

                return (((LogLine)x).LineNumber + 1);
            };

            this.olvcText.AspectGetter = delegate (object x)
            {
                if (((LogLine)x) == null)
                {
                    return "";
                }

                return (lf.GetLine(((LogLine)x).LineNumber));
            };

            menuFileClose.Enabled = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            config.HighlightColour = this.highlightColour.ToKnownColor().ToString();
            string ret = config.Save();
            if (ret.Length > 0)
            {
                UserInterface.DisplayErrorMessageBox(this, ret);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_DragDrop(object sender, DragEventArgs e)
        {
            if (processing == true)
            {
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0)
            {
                return;
            }

            if (files.Length > 1)
            {
                UserInterface.DisplayMessageBox(this, "Only one file can be processed at one time", MessageBoxIcon.Exclamation);
                return;
            }

            LoadFile(files[0]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_DragEnter(object sender, DragEventArgs e)
        {
            if (processing == true)
            {
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
        #endregion

        #region Log File Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private void LoadFile(string filePath)
        {
            this.processing = true;
            this.hourGlass = new HourGlass(this);
            SetProcessingState(false);
            statusProgress.Visible = true;
            this.cancellationTokenSource = new CancellationTokenSource();
            menuToolsMultiStringSearch.Enabled = true;

            // Clear any existing filters/reset values
            this.listLines.ModelFilter = null;
            this.viewMode = Global.ViewMode.Standard;
            this.searches = new Searches();
            this.filterIds.Clear();

            if (lf != null)
            {
                listLines.ClearObjects();
                lf.ProgressUpdate -= LogFile_LoadProgress;
                lf.LoadComplete -= LogFile_LoadComplete;
                lf.SearchComplete -= LogFile_SearchComplete;
                lf.ExportComplete -= LogFile_ExportComplete;
                lf.LoadError -= LogFile_LoadError;
                lf.Dispose();
            }

            this.Text = "LogViewer - " + filePath;

            lf = new LogFile();
            lf.ProgressUpdate += LogFile_LoadProgress;
            lf.LoadComplete += LogFile_LoadComplete;
            lf.SearchComplete += LogFile_SearchComplete;
            lf.ExportComplete += LogFile_ExportComplete;
            lf.LoadError += LogFile_LoadError;
            lf.Load(filePath, cancellationTokenSource.Token);        
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private void SearchFile()
        {
            SearchCriteria sc = new SearchCriteria();
            sc.Type = (Global.SearchType)dropdownSearchType.SelectedIndex;
            sc.Pattern = textSearch.Text;
            sc.Id = searches.Add(sc, toolButtonCumulative.Checked);

            if (sc.Id == 0)
            {
                UserInterface.DisplayMessageBox(this, "The search pattern already exists", MessageBoxIcon.Exclamation);
                return;
            }

            // Add the ID so that any matches show up straight away
            filterIds.Add(sc.Id);

            this.processing = true;
            this.hourGlass = new HourGlass(this);
            SetProcessingState(false);
            statusProgress.Visible = true;
            this.cancellationTokenSource = new CancellationTokenSource();
            lf.Search(sc, toolButtonCumulative.Checked, cancellationTokenSource.Token, config.NumContextLines);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        private void Export(string filePath)
        {
            this.processing = true;
            this.hourGlass = new HourGlass(this);
            SetProcessingState(false);
            statusProgress.Visible = true;
            this.cancellationTokenSource = new CancellationTokenSource();

            if (listLines.ModelFilter == null)
            {
                lf.Export(filePath, cancellationTokenSource.Token);
            }
            else
            {
                lf.Export(listLines.FilteredObjects, filePath, cancellationTokenSource.Token);
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        private void ExportSelected(string filePath)
        {
            this.processing = true;
            this.hourGlass = new HourGlass(this);
            SetProcessingState(false);
            statusProgress.Visible = true;
            this.cancellationTokenSource = new CancellationTokenSource();

            lf.Export(listLines.SelectedObjects, filePath, cancellationTokenSource.Token);
        }
        #endregion

        #region Log File Object Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void LogFile_LoadError(string message)
        {
            UserInterface.DisplayErrorMessageBox(this, message);

            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                this.Text = "LogViewer";
                statusProgress.Visible = false;
                this.hourGlass.Dispose();
                SetProcessingState(true);
                this.cancellationTokenSource.Dispose();               
                this.processing = false;

                // Lets clear the LogFile state and set the UI correctly
                menuFileClose_Click(this, null);

            }), null);        
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percent"></param>
        private void LogFile_LoadProgress(int percent)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                statusProgress.Value = (int)o;
            }), percent);
        }

        /// <summary>
        /// 
        /// </summary>
        private void LogFile_SearchComplete(TimeSpan duration, long matches, int numTerms, bool cancelled)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                statusProgress.Visible = false;
                listLines.Refresh();
                this.hourGlass.Dispose();
                SetProcessingState(true);
                this.cancellationTokenSource.Dispose();
                UpdateStatusLabel("Matched " + matches + " lines (Search Terms: " + numTerms + ") # Duration: " + duration, statusLabelSearch);
                this.processing = false;

            }), null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="val"></param>
        private void LogFile_ExportComplete(TimeSpan duration, bool val)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                statusProgress.Visible = false;
                this.hourGlass.Dispose();
                SetProcessingState(true);
                this.cancellationTokenSource.Dispose();
                UpdateStatusLabel("Export complete # Duration: " + duration, statusLabelSearch);
                this.processing = false;               
            }), null);
        }

        /// <summary>
        /// 
        /// </summary>
        private void LogFile_LoadComplete(TimeSpan duration, bool cancelled)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                listLines.SetObjects(lf.Lines);

                // Try and measure the length of the longest line in pixels
                // This is rough, and tends to be too short, but cannot find
                // another method to make column wide enough :-)
                using (var image = new Bitmap(1, 1))
                {
                    using (var g = Graphics.FromImage(image))
                    {
                        string temp = lf.GetLine(lf.LongestLine.LineNumber);
                        var result = g.MeasureString(temp, new Font("Consolas", 9, FontStyle.Regular, GraphicsUnit.Pixel));
                        olvcText.Width = Convert.ToInt32(result.Width);
                    }
                }

                olvcLineNumber.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
                statusProgress.Visible = false;
                this.hourGlass.Dispose();
                SetProcessingState(true);
                this.cancellationTokenSource.Dispose();
                UpdateStatusLabel(lf.Lines.Count + " Lines # Duration: " + duration, statusLabelMain);
                UpdateStatusLabel("", statusLabelSearch);
                this.processing = false;
                menuFileClose.Enabled = true;

            }), null);           
        }
        #endregion

        #region List Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listLines_FormatRow(object sender, BrightIdeasSoftware.FormatRowEventArgs e)
        {
            if (this.viewMode != Global.ViewMode.FilterHide)
            {
                if ((LogLine)e.Model == null)
                {
                    return;
                }

                if (((LogLine)e.Model).SearchMatches.Intersect(filterIds).Any() == true)
                {
                    e.Item.BackColor = highlightColour;
                }
                else if (((LogLine)e.Model).IsContextLine == true)
                {
                    e.Item.BackColor = contextColour;
                }
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listLines_DragEnter(object sender, DragEventArgs e)
        {
            if (processing == true)
            {
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listLines_DragDrop(object sender, DragEventArgs e)
        {
            if (processing == true)
            {
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0)
            {
                return;
            }

            if (files.Length > 1)
            {
                UserInterface.DisplayMessageBox(this, "Only one file can be processed at one time", MessageBoxIcon.Exclamation);
                return;
            }

            LoadFile(files[0]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listLines_ItemActivate(object sender, EventArgs e)
        {
            if (listLines.SelectedObjects.Count != 1)
            {
                return;
            }

            LogLine ll = (LogLine)listLines.SelectedObjects[0];
            using (FormLine f = new FormLine(this.lf.GetLine(ll.LineNumber)))
            {
                f.ShowDialog(this);
            }
        }
        #endregion

        #region Context Menu Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuFilterClear_Click(object sender, EventArgs e)
        {
            // Get the currently selected row
            var ll = (LogLine)listLines.SelectedObject;

            this.listLines.ModelFilter = null;
            this.viewMode = Global.ViewMode.Standard;

            if (ll != null)
            {
                listLines.EnsureVisible(ll.LineNumber - 1);
                listLines.SelectedIndex = ll.LineNumber - 1;
                if (listLines.SelectedItem != null)
                {
                    listLines.FocusedItem = listLines.SelectedItem;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuFilterShowMatched_Click(object sender, EventArgs e)
        {
            this.viewMode = Global.ViewMode.FilterShow;
            this.listLines.ModelFilter = new ModelFilter(delegate (object x) {
                return x != null && (((LogLine)x).SearchMatches.Intersect(filterIds).Any() == true || (((LogLine)x).IsContextLine == true));
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuFilterHideMatched_Click(object sender, EventArgs e)
        {
            this.viewMode = Global.ViewMode.FilterHide;
            this.listLines.ModelFilter = new ModelFilter(delegate (object x) {
                return x != null && (((LogLine)x).SearchMatches.Intersect(filterIds).Any() == false);
            });
        }

        /// <summary>
        /// Show the Searches window to allow the user to enable/disable search terms
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuSearchViewTerms_Click(object sender, EventArgs e)
        {
            using (FormSearchTerms f = new FormSearchTerms(this.searches))
            {
                DialogResult dr = f.ShowDialog(this);
                if (dr == DialogResult.Cancel)
                {
                    return;
                }

                this.searches = f.Searches;

                filterIds.Clear();
                foreach (SearchCriteria sc in searches.Items)
                {
                    if (sc.Enabled == false)
                    {
                        continue;
                    }

                    filterIds.Add(sc.Id);
                }

                listLines.Refresh();
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuSearchColourMatch_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            DialogResult dr = cd.ShowDialog(this);
            if (dr == DialogResult.Cancel)
            {
                return;
            }

            this.highlightColour = cd.Color;
            listLines.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuSearchColourContext_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            DialogResult dr = cd.ShowDialog(this);
            if (dr == DialogResult.Cancel)
            {
                return;
            }

            this.contextColour = cd.Color;
            listLines.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuExportAll_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "All Files|*.*";
            sfd.FileName = "*.*";
            sfd.Title = "Select export file";

            if (sfd.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }

            Export(sfd.FileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuExportSelected_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "All Files|*.*";
            sfd.FileName = "*.*";
            sfd.Title = "Select export file";

            if (sfd.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }

            ExportSelected(sfd.FileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuCopy_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (LogLine ll in listLines.SelectedObjects)
            {
                sb.AppendLine(lf.GetLine(ll.LineNumber));
            }

            Clipboard.SetText(sb.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextLinesGoToLine_Click(object sender, EventArgs e)
        {
            using (FormGoToLine f = new FormGoToLine())
            {
                DialogResult dr = f.ShowDialog(this);
                if (dr == DialogResult.Cancel)
                {
                    return;
                }

                listLines.EnsureVisible(f.LineNumber - 1);
                var ll = this.lf.Lines.SingleOrDefault(x => x.LineNumber == f.LineNumber);
                if (ll != null)
                {
                    listLines.SelectedIndex = ll.LineNumber - 1;
                    if (listLines.SelectedItem != null)
                    {
                        listLines.FocusedItem = listLines.SelectedItem;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextLinesGoToFirstLine_Click(object sender, EventArgs e)
        {
            listLines.EnsureVisible(0);
            listLines.SelectedIndex = 0;
            if (listLines.SelectedItem != null)
            {
                listLines.FocusedItem = listLines.SelectedItem;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextLinesGoToLastLine_Click(object sender, EventArgs e)
        {
            listLines.EnsureVisible(lf.LineCount - 1);
            listLines.SelectedIndex = lf.LineCount - 1;
            if (listLines.SelectedItem != null)
            {
                listLines.FocusedItem = listLines.SelectedItem;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool enableLineOps = true;
            if (lf == null)
            {
                enableLineOps = false;
            }
            else
            {
                if (lf.LineCount == 0)
                {
                    enableLineOps = false;
                }
            }

            contextLinesGoToFirstLine.Enabled = enableLineOps;
            contextLinesGoToLastLine.Enabled = enableLineOps;
            contextLinesGoToLine.Enabled = enableLineOps;

            if (listLines.SelectedObjects.Count > this.config.MultiSelectLimit)
            {
                contextMenuCopy.Enabled = false;
                contextMenuExportSelected.Enabled = false;
                return;
            }

            contextMenuCopy.Enabled = true;
            contextMenuExportSelected.Enabled = true;
        }
        #endregion

        #region Toolbar Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolButtonSearch_Click(object sender, EventArgs e)
        {
            if (dropdownSearchType.SelectedIndex == -1)
            {
                UserInterface.DisplayMessageBox(this, "The search type is not selected", MessageBoxIcon.Exclamation);
                dropdownSearchType.Select();
                return;
            }

            SearchFile();
        }
        #endregion

        #region Menu Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuFileOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All Files|*.*";
            openFileDialog.FileName = "*.*";
            openFileDialog.Title = "Select log file";

            if (openFileDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }

            LoadFile(openFileDialog.FileName);
        }

        /// <summary>
        /// Close the resources used for opening and processing the log file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuFileClose_Click(object sender, EventArgs e)
        {
            this.Text = "LogViewer";

            // Clear any existing filters/reset values
            this.listLines.ModelFilter = null;
            this.viewMode = Global.ViewMode.Standard;
            this.searches = new Searches();
            this.filterIds.Clear();

            if (lf != null)
            {
                listLines.ClearObjects();
                lf.ProgressUpdate -= LogFile_LoadProgress;
                lf.LoadComplete -= LogFile_LoadComplete;
                lf.SearchComplete -= LogFile_SearchComplete;
                lf.ExportComplete -= LogFile_ExportComplete;
                lf.LoadError -= LogFile_LoadError;
                lf.Dispose();
                lf = null;
            }

            menuFileClose.Enabled = false;
            UpdateStatusLabel("", statusLabelMain);
            UpdateStatusLabel("", statusLabelSearch);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuFileExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuHelpHelp_Click(object sender, EventArgs e)
        {
            Misc.ShellExecuteFile(System.IO.Path.Combine(Misc.GetApplicationDirectory(), "help.pdf"));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuHelpAbout_Click(object sender, EventArgs e)
        {
            using (FormAbout f = new FormAbout())
            {
                f.ShowDialog(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuToolsMultiStringSearch_Click(object sender, EventArgs e)
        {
            using (FormSearch f = new FormSearch(this.searches))
            {
                DialogResult dr = f.ShowDialog(this);
                if (dr == DialogResult.Cancel)
                {
                    return;
                }

                // Clear any existing filter ID's as we will only show the multi-string search
                filterIds.Clear();
                this.searches.Reset();
                foreach (SearchCriteria sc in f.NewSearches)
                {                    
                    // Add the ID so that any matches show up straight away
                    filterIds.Add(sc.Id);
                    this.searches.Add(sc);
                }                

                this.processing = true;
                this.hourGlass = new HourGlass(this);
                SetProcessingState(false);
                statusProgress.Visible = true;
                this.cancellationTokenSource = new CancellationTokenSource();
                lf.SearchMulti(f.NewSearches, cancellationTokenSource.Token, config.NumContextLines);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuToolsConfiguration_Click(object sender, EventArgs e)
        {
            using (FormConfiguration f = new FormConfiguration(this.config))
            {
                f.ShowDialog(this);
            }
        }
        #endregion

        #region UI Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="enabled"></param>
        private void SetProcessingState(bool enabled)
        {
            MethodInvoker methodInvoker = delegate
            {
                menuFileOpen.Enabled = enabled;
                menuFileExit.Enabled = enabled;
                toolButtonCumulative.Enabled = enabled;
                toolButtonSearch.Enabled = enabled;
            };

            if (this.InvokeRequired == true)
            {
                this.BeginInvoke(methodInvoker);
            }
            else
            {
                methodInvoker.Invoke();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enabled"></param>
        private void UpdateStatusLabel(string text, ToolStripStatusLabel control)
        {
            synchronizationContext.Post(new SendOrPostCallback(o =>
            {
                control.Text = (string)o;
            }), text);
        }
        #endregion

        #region Other Control Event Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void statusProgress_Click(object sender, EventArgs e)
        {
            this.cancellationTokenSource.Cancel();          
        }
        #endregion
    }
}
