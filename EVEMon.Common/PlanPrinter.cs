﻿using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using EVEMon.Common.Controls;
using EVEMon.Common.SettingsObjects;

namespace EVEMon.Common
{
    /// <summary>
    /// Prints a plan.
    /// </summary>
    public class PlanPrinter
    {
        private readonly Plan m_plan;
        private readonly Character m_character;
        private readonly Font m_font;
        private readonly Font m_boldFont;
        private readonly SolidBrush m_brush;
        private readonly PlanExportSettings m_settings;

        private int m_entryToPrint;
        private Point m_point;
        private DateTime m_currentDate = DateTime.Now;
        private readonly DateTime m_printStartTime = DateTime.Now;
        private TimeSpan m_trainingTime = TimeSpan.Zero;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="plan">The plan.</param>
        private PlanPrinter(Plan plan)
        {
            m_plan = plan;
            m_plan.UpdateStatistics();

            m_character = (Character)plan.Character;
            m_settings = Settings.Exportation.PlanToText;

            m_brush = new SolidBrush(Color.Black);
            m_font = FontFactory.GetFont("Arial", 10);
            m_boldFont = FontFactory.GetFont("Arial", 10, FontStyle.Bold | FontStyle.Underline);
        }

        /// <summary>
        /// Prints the given plan.
        /// </summary>
        /// <param name="plan"></param>
        public static void Print(Plan plan)
        {
            PlanPrinter printer = new PlanPrinter(plan);
            printer.PrintPlan();
        }

        /// <summary>
        /// Main method
        /// </summary>
        private void PrintPlan()
        {
            PrintDocument doc = new PrintDocument
                                    {
                                        DocumentName =
                                            String.Format(CultureConstants.DefaultCulture, "Skill Plan for {0} ({1})",
                                                          m_character.Name, m_plan.Name)
                                    };
            doc.PrintPage += doc_PrintPage;

            //Display the options
            using (PrintOptionsDialog prdlg = new PrintOptionsDialog(m_settings, doc))
            {
                if (prdlg.ShowDialog() != DialogResult.OK)
                    return;

                doc.PrinterSettings.PrinterName = prdlg.PrinterName;

                // Display the preview
                using (PrintPreviewDialog pd = new PrintPreviewDialog())
                {
                    pd.Document = doc;
                    pd.ShowDialog();
                }
            }
        }

        /// <summary>
        /// Occurs anytime the preview dialog needs to print a page
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Drawing.Printing.PrintPageEventArgs"/> instance containing the event data.</param>
        private void doc_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            string s = String.Format(CultureConstants.DefaultCulture, "Skill Plan for {0} ({1})", m_character.Name, m_plan.Name);
            int index = 0;

            m_point.X = 5;
            m_point.Y = 5;

            // Print header
            if (m_settings.IncludeHeader)
            {
                var size = g.MeasureString(s, m_boldFont);
                m_point.X = (int)((e.MarginBounds.Width - size.Width) / 2);

                size = PrintBold(g, s);
                m_point.Y += (int)(2 * size.Height);
                m_point.X = 5;
            }

            bool resetTotal = true;
            if (m_entryToPrint == 0)
                m_currentDate = m_printStartTime;

            // Scroll through entries
            foreach (var pe in m_plan)
            {
                index++;

                // Not displayed on this page ?
                if (m_entryToPrint >= index)
                {
                    resetTotal = false;
                    continue;
                }

                // Update training time
                if (resetTotal)
                    m_trainingTime = TimeSpan.Zero;

                m_trainingTime += pe.TrainingTime;
                resetTotal = false;

                // Print entry
                PrintEntry(g, index, pe);

                // End of page ?
                if (m_point.Y > e.MarginBounds.Bottom)
                {
                    m_entryToPrint = index;
                    e.HasMorePages = true;
                    return;
                }

                m_entryToPrint = 0;
            }

            // Reached the end of the plan
            e.HasMorePages = false;
            m_entryToPrint = 0;

            // Print footer
            PrintPageFooter(g, index);
        }

        /// <summary>
        /// Prints a single entry
        /// </summary>
        /// <param name="g">The graphics canvas.</param>
        /// <param name="index">The index.</param>
        /// <param name="pe">The plan entry.</param>
        private void PrintEntry(Graphics g, int index, PlanEntry pe)
        {
            SizeF size;

            // Print entry index
            if (m_settings.EntryNumber)
            {
                size = Print(g, index.ToString() + ": ");
                m_point.X += (int)size.Width;
            }

            // Print skill name and level
            size = PrintBold(g, pe.ToString());
            m_point.X += (int)size.Width;

            // Print Notes ?
            if (m_settings.EntryNotes)
            {
                // Jump to next line
                m_point.Y += (int)size.Height;
                m_point.X = 20;

                // Note
                size = Print(g, pe.Notes);
                m_point.X += (int)size.Width;
            }

            // Print additional infos below
            if (m_settings.EntryTrainingTimes || m_settings.EntryStartDate || m_settings.EntryFinishDate)
            {
                // Jump to next line
                m_point.Y += (int)size.Height;
                m_point.X = 20;

                // Open parenthesis
                size = Print(g, " (");
                m_point.X += (int)size.Width;

                // Training time ?
                bool needComma = false;
                if (m_settings.EntryTrainingTimes)
                {
                    size = Print(g, pe.TrainingTime.ToDescriptiveText(
                        DescriptiveTextOptions.FullText |
                        DescriptiveTextOptions.IncludeCommas |
                        DescriptiveTextOptions.SpaceText));
                    m_point.X += (int)size.Width;
                    needComma = true;
                }

                // Start date ?
                if (m_settings.EntryStartDate)
                {
                    if (needComma)
                    {
                        size = Print(g, "; ");
                        m_point.X += (int)size.Width;
                    }

                    size = Print(g, "Start: ");
                    m_point.X += (int)size.Width;

                    size = Print(g, pe.StartTime.ToString());
                    m_point.X += (int)size.Width;

                    needComma = true;
                }

                // End date ?
                if (m_settings.EntryFinishDate)
                {
                    if (needComma)
                    {
                        size = Print(g, "; ");
                        m_point.X += (int)size.Width;
                    }
                    size = Print(g, "Finish: ");
                    m_point.X += (int)size.Width;

                    size = Print(g, pe.EndTime.ToString());
                    m_point.X += (int)size.Width;
                }

                // Close parenthesis
                size = Print(g, ")");
                m_point.X += (int)size.Width;
            }

            // Jump to next line
            m_point.X = 5;
            m_point.Y += (int)size.Height;
        }

        /// <summary>
        /// Prints the footer displaying the statistics for this page ONLY
        /// </summary>
        /// <param name="g">The graphis canvas.</param>
        /// <param name="index">The index.</param>
        private void PrintPageFooter(Graphics g, int index)
        {
            SizeF size = SizeF.Empty;
            bool needComma = false;

            if (!m_settings.FooterCount && !m_settings.FooterTotalTime && !m_settings.FooterDate)
                return;

            // Jump to next line
            m_point.X = 5;
            m_point.Y += 20;

            // Total number of entries on this page
            if (m_settings.FooterCount)
            {
                size = Print(g, index.ToString());
                m_point.X += (int)size.Width;

                size = Print(g, index != 1 ? " skills" : " skill");

                m_point.X += (int)size.Width;
                needComma = true;
            }

            // Total training time for ths page
            if (m_settings.FooterTotalTime)
            {
                if (needComma)
                {
                    size = Print(g, "; ");
                    m_point.X += (int)size.Width;
                }
                size = Print(g, "Total time: ");
                m_point.X += (int)size.Width;

                size = Print(g, m_trainingTime.ToDescriptiveText(
                    DescriptiveTextOptions.FullText
                    | DescriptiveTextOptions.IncludeCommas
                    | DescriptiveTextOptions.SpaceText));

                m_point.X += (int)size.Width;

                needComma = true;
            }

            // Date at the end of this plan
            if (m_settings.FooterDate)
            {
                if (needComma)
                {
                    size = Print(g, "; ");
                    m_point.X += (int)size.Width;
                }
                size = Print(g, "Completion: ");
                m_point.X += (int)size.Width;
                size = Print(g, m_currentDate.ToString());
                m_point.X += (int)size.Width;
            }

            // Jump line
            m_point.X = 5;
            m_point.Y += (int)size.Height;
        }

        /// <summary>
        /// Measures and prints using the bold font.
        /// </summary>
        /// <param name="g">The graphics canvas.</param>
        /// <param name="s">The string to print.</param>
        /// <returns></returns>
        private SizeF PrintBold(Graphics g, string s)
        {
            SizeF f = g.MeasureString(s, m_boldFont);
            g.DrawString(s, m_boldFont, m_brush, m_point);
            return f;
        }

        /// <summary>
        /// Measures and prints using the regular font.
        /// </summary>
        /// <param name="g">The graphics canvas.</param>
        /// <param name="s">The string to print.</param>
        /// <returns></returns>
        private SizeF Print(Graphics g, string s)
        {
            SizeF f = g.MeasureString(s, m_font);
            g.DrawString(s, m_font, m_brush, m_point);
            return f;
        }
    }
}