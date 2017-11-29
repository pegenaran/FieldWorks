﻿// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Areas.Lists;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Filters;
using SIL.FieldWorks.Resources;
using LanguageExplorer.Works;
using SIL.LCModel;
using SIL.LCModel.Application;

namespace LanguageExplorer.Areas.Grammar.Tools.PosEdit
{
	/// <summary>
	/// ITool implementation for the "posEdit" tool in the "grammar" area.
	/// </summary>
	[Export(AreaServices.GrammarAreaMachineName, typeof(ITool))]
	internal sealed class PosEditTool : ITool
	{
		private GrammarAreaMenuHelper _grammarAreaWideMenuHelper;
		private const string Categories_withTreeBarHandler = "categories_withTreeBarHandler";
		/// <summary>
		/// Main control to the right of the side bar control. This holds a RecordBar on the left and a PaneBarContainer on the right.
		/// The RecordBar has no top PaneBar for information, menus, etc.
		/// </summary>
		private CollapsingSplitContainer _collapsingSplitContainer;
		private IRecordClerk _recordClerk;
		[Import(AreaServices.GrammarAreaMachineName)]
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
			_grammarAreaWideMenuHelper.Dispose();
			CollapsingSplitContainerFactory.RemoveFromParentAndDispose(majorFlexComponentParameters.MainCollapsingSplitContainer, ref _collapsingSplitContainer);
			_grammarAreaWideMenuHelper = null;
		}

		/// <summary>
		/// Activate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the component that is becoming active.
		/// </remarks>
		public void Activate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			majorFlexComponentParameters.FlexComponentParameters.PropertyTable.SetDefault($"{AreaServices.ToolForAreaNamed_}{_area.MachineName}", MachineName, SettingsGroup.LocalSettings, true, false);
			majorFlexComponentParameters.FlexComponentParameters.PropertyTable.SetDefault("PartsOfSpeech.posEdit.DataTree-Splitter", 200, SettingsGroup.LocalSettings, true, false);
			majorFlexComponentParameters.FlexComponentParameters.PropertyTable.SetDefault("PartsOfSpeech.posAdvancedEdit.DataTree-Splitter", 200, SettingsGroup.LocalSettings, true, false);

			if (_recordClerk == null)
			{
				_recordClerk = majorFlexComponentParameters.RecordClerkRepositoryForTools.GetRecordClerk(Categories_withTreeBarHandler, majorFlexComponentParameters.Statusbar, FactoryMethod);
			}
			_grammarAreaWideMenuHelper = new GrammarAreaMenuHelper(majorFlexComponentParameters, _recordClerk); // Use generic export event handler.

			var dataTree = new DataTree();
#if RANDYTODO
			// TODO: See LexiconEditTool for how to set up all manner of menus and toolbars.
#endif
			_collapsingSplitContainer = CollapsingSplitContainerFactory.Create(majorFlexComponentParameters.FlexComponentParameters, majorFlexComponentParameters.MainCollapsingSplitContainer, true,
				XDocument.Parse(ListResources.PosEditParameters).Root, XDocument.Parse(AreaResources.HideAdvancedListItemFields),
				MachineName,
				majorFlexComponentParameters.LcmCache,
				_recordClerk,
				dataTree,
				MenuServices.GetFilePrintMenu(majorFlexComponentParameters.MenuStrip));
			RecordClerkServices.SetClerk(majorFlexComponentParameters, _recordClerk);
		}

		/// <summary>
		/// Do whatever might be needed to get ready for a refresh.
		/// </summary>
		public void PrepareToRefresh()
		{
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
		public string MachineName => AreaServices.PosEditMachineName;

		/// <summary>
		/// User-visible localizable component name.
		/// </summary>
		public string UiName => "Category Edit";

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area for the tool.
		/// </summary>
		public IArea Area => _area;

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon => Images.EditView.SetBackgroundColor(Color.Magenta);

		#endregion

		private static IRecordClerk FactoryMethod(LcmCache cache, FlexComponentParameters flexComponentParameters, string clerkId, StatusBar statusBar)
		{
			Require.That(clerkId == Categories_withTreeBarHandler, $"I don't know how to create a clerk with an ID of '{clerkId}', as I can only create on with an id of '{Categories_withTreeBarHandler}'.");

			return new RecordClerk(clerkId,
				statusBar,
				new PossibilityRecordList(cache.ServiceLocator.GetInstance<ISilDataAccessManaged>(), cache.LanguageProject.PartsOfSpeechOA),
				new PropertyRecordSorter("ShortName"),
				"Default",
				null,
				false,
				false,
				new PossibilityTreeBarHandler(flexComponentParameters.PropertyTable, true, true, false, "best analorvern"));
		}
	}
}