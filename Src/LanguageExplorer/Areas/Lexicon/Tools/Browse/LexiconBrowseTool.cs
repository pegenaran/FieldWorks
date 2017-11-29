﻿// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.ComponentModel.Composition;
using System.Drawing;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using SIL.FieldWorks.Resources;
using LanguageExplorer.Works;
using SIL.LCModel.Application;

namespace LanguageExplorer.Areas.Lexicon.Tools.Browse
{
	/// <summary>
	/// ITool implementation for the "lexiconBrowse" tool in the "lexicon" area.
	/// </summary>
	[Export(AreaServices.LexiconAreaMachineName, typeof(ITool))]
	internal sealed class LexiconBrowseTool : ITool
	{
		private LexiconAreaMenuHelper _lexiconAreaMenuHelper;
		private PaneBarContainer _paneBarContainer;
		private RecordBrowseView _recordBrowseView;
		private IRecordClerk _recordClerk;
		[Import(AreaServices.LexiconAreaMachineName)]
		private IArea _area;

		#region Implementation of IMajorFlexComponent

		/// <summary>
		/// Deactivate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the outgoing component, when the user switches to a component.
		/// </remarks>
		public void Deactivate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			_lexiconAreaMenuHelper.Dispose();
			PaneBarContainerFactory.RemoveFromParentAndDispose(majorFlexComponentParameters.MainCollapsingSplitContainer, ref _paneBarContainer);
			_recordBrowseView = null;
			_lexiconAreaMenuHelper = null;
		}

		/// <summary>
		/// Activate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the component that is becoming active.
		/// </remarks>
		public void Activate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			if (_recordClerk == null)
			{
				_recordClerk = majorFlexComponentParameters.RecordClerkRepositoryForTools.GetRecordClerk(LexiconArea.Entries, majorFlexComponentParameters.Statusbar, LexiconArea.EntriesFactoryMethod);
			}
			_lexiconAreaMenuHelper = new LexiconAreaMenuHelper(majorFlexComponentParameters, _recordClerk);

			var root = XDocument.Parse(LexiconResources.LexiconBrowseParameters).Root;
			var columnsElement = XElement.Parse(LexiconResources.LexiconBrowseDialogColumnDefinitions);
			OverrideServices.OverrideVisibiltyAttributes(columnsElement, XElement.Parse(LexiconResources.LexiconBrowseOverrides));
			root.Add(columnsElement);
			_recordBrowseView = new RecordBrowseView(root, majorFlexComponentParameters.LcmCache, _recordClerk);
			_paneBarContainer = PaneBarContainerFactory.Create(
				majorFlexComponentParameters.FlexComponentParameters,
				majorFlexComponentParameters.MainCollapsingSplitContainer,
				_recordBrowseView);
			RecordClerkServices.SetClerk(majorFlexComponentParameters, _recordClerk);
			_lexiconAreaMenuHelper.Initialize();
		}

		/// <summary>
		/// Do whatever might be needed to get ready for a refresh.
		/// </summary>
		public void PrepareToRefresh()
		{
			_recordBrowseView.BrowseViewer.BrowseView.PrepareToRefresh();
		}

		/// <summary>
		/// Finish the refresh.
		/// </summary>
		public void FinishRefresh()
		{
			_recordClerk.ReloadIfNeeded();
			((DomainDataByFlidDecoratorBase)_recordClerk.VirtualListPublisher).Refresh();
		}

		/// <summary>
		/// The properties are about to be saved, so make sure they are all current.
		/// Add new ones, as needed.
		/// </summary>
		public void EnsurePropertiesAreCurrent()
		{
		}

		#endregion

		#region Implementation of IMajorFlexUiComponent

		/// <summary>
		/// Get the internal name of the component.
		/// </summary>
		/// <remarks>NB: This is the machine friendly name, not the user friendly name.</remarks>
		public string MachineName => AreaServices.LexiconBrowseMachineName;

		/// <summary>
		/// User-visible localizable component name.
		/// </summary>
		public string UiName => "Browse";
		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area for the tool.
		/// </summary>
		public IArea Area => _area;

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon => Images.BrowseView.SetBackgroundColor(Color.Magenta);

		#endregion
	}
}