// Copyright (c) 2014-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Windows.Forms;
using LanguageExplorer.Controls.LexText;
using LanguageExplorer.Controls.XMLViews;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FwCoreDlgs;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
	/// <summary />
	internal class AddPrimaryLexemeChooserCommand : ChooserCommand
	{
		private readonly ILexEntryRef m_lexEntryRef;
		private readonly Form m_parentWindow;

		/// <summary />
		public AddPrimaryLexemeChooserCommand(LcmCache cache, bool fCloseBeforeExecuting,
			string sLabel, IPropertyTable propertyTable, IPublisher publisher, ISubscriber subscriber, ICmObject lexEntryRef, /* Why ICmObject? */
			Form parentWindow)
			: base(cache, fCloseBeforeExecuting, sLabel, propertyTable, publisher, subscriber)
		{
			m_lexEntryRef = lexEntryRef as ILexEntryRef;
			m_parentWindow = parentWindow;
		}

		/// <summary />
		public override ObjectLabel Execute()
		{
			ObjectLabel result = null;
			if (m_lexEntryRef != null)
			{
				using (LinkEntryOrSenseDlg dlg = new LinkEntryOrSenseDlg())
				{
					dlg.InitializeFlexComponent(new FlexComponentParameters(m_propertyTable, m_publisher, m_subscriber));
					ILexEntry le = null;
					// assume the owner is the entry (e.g. owner of LexEntryRef)
					le = m_lexEntryRef.OwnerOfClass<ILexEntry>();
					dlg.SetDlgInfo(m_cache, le);
					dlg.SetHelpTopic("khtpChooseLexicalEntryOrSense");
					if (dlg.ShowDialog(m_parentWindow) == DialogResult.OK)
					{
						ICmObject obj = dlg.SelectedObject;
						if (obj != null)
						{
							if (!m_lexEntryRef.PrimaryLexemesRS.Contains(obj))
							{
								try
								{
									UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(
										LanguageExplorerResources.ksUndoCreatingEntry,
										LanguageExplorerResources.ksRedoCreatingEntry,
										Cache.ActionHandlerAccessor,
										() =>
										{
											if (!m_lexEntryRef.ComponentLexemesRS.Contains(obj))
												m_lexEntryRef.ComponentLexemesRS.Add(obj);
											m_lexEntryRef.PrimaryLexemesRS.Add(obj);
										});
								}
								catch (ArgumentException)
								{
									MessageBoxes.ReportLexEntryCircularReference(m_lexEntryRef.Owner, obj, true);
								}
							}
						}
					}
				}
			}
			return result;
		}
	}
}