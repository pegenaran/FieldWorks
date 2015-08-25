﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.Widgets;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.XWorks;
using SIL.Utils;

namespace SIL.FieldWorks.IText
{
	public partial class StatisticsView : UserControl, IMainContentControl, IFWDisposable
	{
		private bool _shouldNotCall;

		private string _areaName;
		private FdoCache _cache;
		private InterlinearTextsRecordClerk m_clerk;

		public StatisticsView()
		{
			InitializeComponent();

			ContextMenu cm = new ContextMenu();

			var mi = new MenuItem("Copy");
			mi.Click += new EventHandler(mi_Copy);
			cm.MenuItems.Add(mi);

			statisticsBox.ContextMenu = cm;
		}

		#region Implementation of IPropertyTableProvider

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; private set; }

		#endregion

		#region Implementation of IPublisherProvider

		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		public IPublisher Publisher { get; private set; }

		#endregion

		#region Implementation of ISubscriberProvider

		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		public ISubscriber Subscriber { get; private set; }

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="propertyTable">Interface to a property table.</param>
		/// <param name="publisher">Interface to the publisher.</param>
		/// <param name="subscriber">Interface to the subscriber.</param>
		public void InitializeFlexComponent(IPropertyTable propertyTable, IPublisher publisher, ISubscriber subscriber)
		{
			FlexComponentCheckingService.CheckInitializationValues(propertyTable, publisher, subscriber, PropertyTable, Publisher, Subscriber);

			PropertyTable = propertyTable;
			Publisher = publisher;
			Subscriber = subscriber;

			_cache = PropertyTable.GetValue<FdoCache>("cache");

#if RANDYTODO
			var name = XmlUtils.GetAttributeValue(configurationParameters, "clerk");
#else
			var name = "FIND_CLERK_Name";
#endif
			var clerk = RecordClerk.FindClerk(PropertyTable, name);
			m_clerk = (clerk == null || clerk is TemporaryRecordClerk) ?
				(InterlinearTextsRecordClerk)RecordClerkFactory.CreateClerk(PropertyTable, Publisher, Subscriber, true) :
				(InterlinearTextsRecordClerk)clerk;
			// There's no record bar for it to control, but it should control the staus bar (e.g., it should update if we change
			// the set of selected texts).
			m_clerk.ActivateUI(true);
#if RANDYTODO
			// TODO: Get area name some other way.
			_areaName = XmlUtils.GetOptionalAttributeValue(configurationParameters, "area", "unknown");
#endif
			RebuildStatisticsTable();
			//add our current state to the history system
			string toolName = PropertyTable.GetValue("currentContentControl", "");
			Publisher.Publish("AddContextToHistory", new FwLinkArgs(toolName, Guid.Empty));
		}

		#endregion

		public string AccName
		{
			get { return ITextStrings.ksTextAreaStatisticsViewName; }
			set { /* Do nothing. */ ; }
		}

		/// <summary>
		/// Get/set string that will trigger a message box to show.
		/// </summary>
		/// <remarks>Set to null or string.Empty to not show the message box.</remarks>
		public string MessageBoxTrigger { get; set; }

		#region Implementation of ICtrlTabProvider

		/// <summary>
		/// Gather up suitable targets to Cntrl-(Shift-)Tab into.
		/// </summary>
		/// <param name="targetCandidates">List of places to move to.</param>
		/// <returns>A suitable target for moving to in Ctrl(+Shift)+Tab.
		/// This returned value should also have been added to the main list.</returns>
		public Control PopulateCtrlTabTargetCandidateList(List<Control> targetCandidates)
		{
			targetCandidates.Add(this);
			return this;
		}

		#endregion

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification="REVIEW: I'm not sure if/where Font gets disposed)")]
		[SuppressMessage("Gendarme.Rules.Portability", "MonoCompatibilityReviewRule",
			Justification="See TODO-Linux comment")]
		private void RebuildStatisticsTable()
		{
			statisticsBox.Clear();
			// TODO-Linux: SelectionTabs isn't implemented on Mono
			statisticsBox.SelectionTabs = new int[] { 10, 300};
			//retrieve the default UI font.
			var font = FontHeightAdjuster.GetFontForStyle(StyleServices.NormalStyleName,
														  FontHeightAdjuster.StyleSheetFromPropertyTable(PropertyTable),
														  Cache.DefaultUserWs, Cache.WritingSystemFactory);
			//increase the size of the default UI font and make it bold for the header.
			Font headerFont = new Font(font.FontFamily, font.SizeInPoints + 1.0f, FontStyle.Bold, font.Unit, font.GdiCharSet);

			//refresh the statisticsDescription (in case of font changes)
			statisticsBox.Text = ITextStrings.ksStatisticsView_HeaderText;

			int row = 0;
			var textList = InterestingTextsDecorator.GetInterestingTextList(PropertyTable, Cache.ServiceLocator);
			int numberOfSegments = 0;
			int wordCount = 0;
			int uniqueWords = 0;

			Dictionary<int, int> languageCount = new Dictionary<int, int>();
			Dictionary<int, Set<String>> languageTypeCount = new Dictionary<int, Set<String>>();
			//for each interesting text
			foreach (var text in textList.InterestingTexts)
			{
				//if a text is deleted in Interlinear there could be a text in this list which has invalid data.
				if (text.Hvo < 0)
					continue;
				//for every paragraph in the interesting text
				for (int index = 0; index < text.ParagraphsOS.Count; ++index)
				{
					//count the segments in this paragraph
					numberOfSegments += text[index].SegmentsOS.Count;
					//count all the things analyzed as words
					var words = new List<IAnalysis>(text[index].Analyses);
					foreach (var word in words)
					{
						var wordForm = word.Wordform;
						if (wordForm != null)
						{
							var valdWSs = wordForm.Form.AvailableWritingSystemIds;
							foreach (var ws in valdWSs)
							{
								// increase the count of words(tokens) for this language
								int count = 0;
								if (languageCount.TryGetValue(ws, out count))
								{
									languageCount[ws] = count + 1;
								}
								else
								{
									languageCount.Add(ws, 1);
								}
								//increase the count of unique words(types) for this language
								Set<String> pair;
								if (languageTypeCount.TryGetValue(ws, out pair))
								{
									//add the string for this writing system in all lower case to the set, unique count is case insensitive
									pair.Add(word.Wordform.Form.get_String(ws).Text.ToLower());
								}
								else
								{
									//add the string for this writing system in all lower case to the set, unique count is case insensitive
									languageTypeCount.Add(ws, new Set<String> { word.Wordform.Form.get_String(ws).Text.ToLower() });
								}
							}
						}
					}
					words.RemoveAll(item => !item.HasWordform);
					wordCount += words.Count;
				}
			}
			// insert total word type count
			statisticsBox.Text += Environment.NewLine + Environment.NewLine + Environment.NewLine + "\t" + ITextStrings.ksStatisticsViewTotalWordTypesText + "\t"; // Todo: find the right System.?.NewLine constant

			++row;
			//add one row for the unique words in each language.
			foreach (KeyValuePair<int, Set<String>> keyValuePair in languageTypeCount)
			{
				var ws = Cache.WritingSystemFactory.get_EngineOrNull(keyValuePair.Key);

				string labText = (ws != null ? ws.ToString() : "#unknown#") + @":";
				statisticsBox.Text += Environment.NewLine + Environment.NewLine + "\t" + labText + "\t"; // Todo: find the right System.?.NewLine constant
				statisticsBox.Text += "" + keyValuePair.Value.Count;
				++row;
				uniqueWords += keyValuePair.Value.Count; //increase the total of unique words
			}

			// next insert the word count.
			statisticsBox.Text += Environment.NewLine + Environment.NewLine + Environment.NewLine + "\t" + ITextStrings.ksStatisticsViewTotalWordTokensText + "\t"; // Todo: find the right System.?.NewLine constant
			statisticsBox.Text += wordCount;
			++row;
			//add one row for the token count for each language.
			foreach (KeyValuePair<int, int> keyValuePair in languageCount)
			{
				var ws = Cache.WritingSystemFactory.get_EngineOrNull(keyValuePair.Key);

				string labText = (ws != null ? ws.ToString() : "#unknown#") + @":";
				statisticsBox.Text += Environment.NewLine + Environment.NewLine + "\t" + labText + "\t"; // Todo: find the right System.?.NewLine constant
				statisticsBox.Text += "" + keyValuePair.Value;
				++row;
			}
			statisticsBox.Text += Environment.NewLine + Environment.NewLine + Environment.NewLine + "\t" + ITextStrings.ksStatisticsViewTotalSentencesText + "\t"; // Todo: find the right System.?.NewLine constant

			// next insert the sentence count.
			statisticsBox.Text += numberOfSegments;

			// insert the total word type count into the richTextBox (it wasn't available earlier)
			statisticsBox.SelectionStart = statisticsBox.Find(ITextStrings.ksStatisticsViewTotalWordTypesText) +
										   ITextStrings.ksStatisticsViewTotalWordTypesText.Length;
			statisticsBox.SelectionLength = 1;
			statisticsBox.SelectedText = "\t" + uniqueWords;

			// Set the font for the header. Do this after we add the other stuff to make sure
			// it doesn't apply to extra text added adjacent to it.
			statisticsBox.Select(0, ITextStrings.ksStatisticsView_HeaderText.Length);
			statisticsBox.SelectionFont = headerFont;
			statisticsBox.Select(0, 0);
		}

#if RANDYTODO
		public bool OnDisplayAddTexts(object commandObject, ref UIItemDisplayProperties display)
		{
			CheckDisposed();
			display.Enabled = m_clerk != null;
			display.Visible = display.Enabled;
			return true;
		}
#endif

		public bool OnAddTexts(object args)
		{
			bool result = m_clerk.OnAddTexts(args);
			if(result)
			{
				RebuildStatisticsTable();
			}
			return result;
		}

		#region Implementation of IMainContentControl

		public bool PrepareToGoAway()
		{
			CheckDisposed();

			return true;
		}

		public string AreaName
		{
			get { return _areaName; }
		}

		#endregion

		#region Implementation of IFWDisposable

		/// <summary>
		/// This method throws an ObjectDisposedException if IsDisposed returns
		/// true.  This is the case where a method or property in an object is being
		/// used but the object itself is no longer valid.
		///
		/// This method should be added to all public properties and methods of this
		/// object and all other objects derived from it (extensive).
		/// </summary>
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException("StatisticsView has been disposed.");
		}

		#endregion

		/// <summary>
		/// FDO cache.
		/// </summary>
		protected FdoCache Cache
		{
			get
			{
				return _cache;
			}
		}
		//Code to add right click


		void mi_Copy(object sender, EventArgs e)
		{
			statisticsBox.Copy();
		}
	}
}
