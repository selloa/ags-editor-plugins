namespace AGS.Plugin.SpeechViewer
{
    partial class SpeechViewerPane
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnRefresh = new System.Windows.Forms.ToolStripButton();
            this.btnExportHtml = new System.Windows.Forms.ToolStripButton();
            this.lblFilter = new System.Windows.Forms.ToolStripLabel();
            this.txtFilter = new System.Windows.Forms.ToolStripTextBox();
            this.lblStats = new System.Windows.Forms.ToolStripLabel();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.treeNavigation = new System.Windows.Forms.TreeView();
            this.gridSpeech = new System.Windows.Forms.DataGridView();
            this.colLineId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSpeaker = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNotes = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblFooter = new System.Windows.Forms.Label();
            this.toolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridSpeech)).BeginInit();
            this.SuspendLayout();
            //
            // toolStrip
            //
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnRefresh,
            this.btnExportHtml,
            this.lblFilter,
            this.txtFilter,
            this.lblStats});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(860, 25);
            this.toolStrip.TabIndex = 0;
            //
            // btnRefresh
            //
            this.btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(50, 22);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            //
            // btnExportHtml
            //
            this.btnExportHtml.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnExportHtml.Name = "btnExportHtml";
            this.btnExportHtml.Size = new System.Drawing.Size(86, 22);
            this.btnExportHtml.Text = "Export HTML...";
            this.btnExportHtml.Click += new System.EventHandler(this.btnExportHtml_Click);
            //
            // lblFilter
            //
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size(36, 22);
            this.lblFilter.Text = "Filter:";
            //
            // txtFilter
            //
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new System.Drawing.Size(140, 25);
            this.txtFilter.TextChanged += new System.EventHandler(this.txtFilter_TextChanged);
            //
            // lblStats
            //
            this.lblStats.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.lblStats.Name = "lblStats";
            this.lblStats.Size = new System.Drawing.Size(0, 22);
            //
            // splitContainer
            //
            this.splitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer.Location = new System.Drawing.Point(0, 25);
            this.splitContainer.Name = "splitContainer";
            //
            // splitContainer.Panel1
            //
            this.splitContainer.Panel1.Controls.Add(this.treeNavigation);
            //
            // splitContainer.Panel2
            //
            this.splitContainer.Panel2.Controls.Add(this.gridSpeech);
            this.splitContainer.Size = new System.Drawing.Size(860, 415);
            this.splitContainer.SplitterDistance = 220;
            this.splitContainer.TabIndex = 1;
            //
            // treeNavigation
            //
            this.treeNavigation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeNavigation.HideSelection = false;
            this.treeNavigation.Location = new System.Drawing.Point(0, 0);
            this.treeNavigation.Name = "treeNavigation";
            this.treeNavigation.Size = new System.Drawing.Size(220, 415);
            this.treeNavigation.TabIndex = 0;
            this.treeNavigation.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeNavigation_AfterSelect);
            //
            // gridSpeech
            //
            this.gridSpeech.AllowUserToAddRows = false;
            this.gridSpeech.AllowUserToDeleteRows = false;
            this.gridSpeech.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.gridSpeech.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridSpeech.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colLineId,
            this.colSpeaker,
            this.colText,
            this.colNotes});
            this.gridSpeech.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridSpeech.Location = new System.Drawing.Point(0, 0);
            this.gridSpeech.MultiSelect = false;
            this.gridSpeech.Name = "gridSpeech";
            this.gridSpeech.ReadOnly = true;
            this.gridSpeech.RowHeadersVisible = false;
            this.gridSpeech.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridSpeech.Size = new System.Drawing.Size(536, 415);
            this.gridSpeech.TabIndex = 0;
            //
            // colLineId
            //
            this.colLineId.HeaderText = "Line ID";
            this.colLineId.Name = "colLineId";
            this.colLineId.ReadOnly = true;
            this.colLineId.Width = 140;
            //
            // colSpeaker
            //
            this.colSpeaker.HeaderText = "Speaker";
            this.colSpeaker.Name = "colSpeaker";
            this.colSpeaker.ReadOnly = true;
            this.colSpeaker.Width = 120;
            //
            // colText
            //
            this.colText.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colText.HeaderText = "Text";
            this.colText.Name = "colText";
            this.colText.ReadOnly = true;
            //
            // colNotes
            //
            this.colNotes.HeaderText = "Notes";
            this.colNotes.Name = "colNotes";
            this.colNotes.ReadOnly = true;
            this.colNotes.Width = 120;
            //
            // lblFooter
            //
            this.lblFooter.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFooter.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFooter.Location = new System.Drawing.Point(3, 443);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(854, 32);
            this.lblFooter.TabIndex = 2;
            this.lblFooter.Text = "Default language only; literals only. Filter uses AND-token matching across the current list (same idea as exported HTML search).";
            //
            // SpeechViewerPane
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblFooter);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.toolStrip);
            this.Name = "SpeechViewerPane";
            this.Size = new System.Drawing.Size(860, 480);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridSpeech)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripButton btnExportHtml;
        private System.Windows.Forms.ToolStripLabel lblFilter;
        private System.Windows.Forms.ToolStripTextBox txtFilter;
        private System.Windows.Forms.ToolStripLabel lblStats;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TreeView treeNavigation;
        private System.Windows.Forms.DataGridView gridSpeech;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLineId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSpeaker;
        private System.Windows.Forms.DataGridViewTextBoxColumn colText;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNotes;
        private System.Windows.Forms.Label lblFooter;
    }
}
