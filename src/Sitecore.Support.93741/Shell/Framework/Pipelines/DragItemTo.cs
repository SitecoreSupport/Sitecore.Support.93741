using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Web.UI.Sheer;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Links;
using Sitecore.Shell.Framework;
using Sitecore.Shell.Framework.Pipelines;

namespace Sitecore.Support.Shell.Framework.Pipelines
{
    public class DragItemTo : ItemOperation
    {
        public void CheckLanguage(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (args.Parameters["copy"] == "1")
            {
                if (args.IsPostBack)
                {
                    if (args.Result != "yes")
                    {
                        args.AbortPipeline();
                    }
                    args.IsPostBack = false;
                }
                else
                {
                    Database database = GetDatabase(args);
                    if (GetSource(args, database).TemplateID == TemplateIDs.Language)
                    {
                        SheerResponse.Confirm("You are coping a language.\n\nA language item must have a name that is a valid ISO-code.\n\nPlease rename the copied item afterward.\n\nAre you sure you want to continue?");
                        args.WaitForPostBack();
                    }
                }
            }
        }

        public void CheckLinks(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (args.Parameters["copy"] != "1")
            {
                if (args.IsPostBack)
                {
                    if (args.Result != "yes")
                    {
                        args.AbortPipeline();
                    }
                }
                else
                {
                    Database database = GetDatabase(args);
                    if (ItemOperation.GetLinks(GetSource(args, database)) > 250)
                    {
                        SheerResponse.Confirm(Translate.Text("This operation may take a long time to complete.\n\nAre you sure you want to continue?"));
                        args.WaitForPostBack();
                    }
                }
            }
        }

        public void CheckPermissions(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (SheerResponse.CheckModified())
            {
                Database database = GetDatabase(args);
                Item target = GetTarget(args);
                Item source = GetSource(args, database);
                if (source.ID == target.ID)
                {
                    Context.ClientPage.ClientResponse.Alert("You cannot drag an item onto itself.");
                    args.AbortPipeline();
                }
                else if (source.Axes.IsAncestorOf(target))
                {
                    Context.ClientPage.ClientResponse.Alert("You cannot drag an item to a subitem.");
                    args.AbortPipeline();
                }
                else if ((args.Parameters["copy"] != "1") && source.Appearance.ReadOnly)
                {
                    Context.ClientPage.ClientResponse.Alert("You cannot move a protected item.");
                    args.AbortPipeline();
                }
                else if (!target.Access.CanCreate())
                {
                    Context.ClientPage.ClientResponse.Alert("You do not have permission to create items here.");
                    args.AbortPipeline();
                }
                else if (args.Parameters["copy"] == "1")
                {
                    if (!source.Access.CanCopyTo(target))
                    {
                        Context.ClientPage.ClientResponse.Alert("You do not have permission to copy the item to the new location.");
                        args.AbortPipeline();
                    }
                }
                else if (!source.Access.CanMoveTo(target))
                {
                    Context.ClientPage.ClientResponse.Alert("You do not have permission to move the item to the new location");
                    args.AbortPipeline();
                }
            }
        }

        public void CheckShadows(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (args.IsPostBack)
            {
                if (args.Result == "yes")
                {
                    args.Result = string.Empty;
                    args.IsPostBack = false;
                }
                else if (args.Result == "no")
                {
                    args.AbortPipeline();
                }
            }
            else
            {
                Database database = GetDatabase(args);
                Item target = GetTarget(args);
                Item source = GetSource(args, database);
                if (!IsSameDatabases(target, source))
                {
                    Context.ClientPage.ClientResponse.Alert("The item is from another database, and you cannot move\nan item outside its database.");
                    args.AbortPipeline();
                }
                else if (source.RuntimeSettings.IsVirtual || source.Database.DataManager.HasShadows(source))
                {
                    string text = Translate.Text("This item also occurs in other locations. If you move it,\nit maybe deleted from the other locations.\n\nAre you sure you want to move '{0}'?", new object[] { source.Name });
                    Context.ClientPage.ClientResponse.Confirm(text);
                    args.WaitForPostBack();
                }
            }
        }

        public void Confirm(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (args.Result == "yes")
            {
                args.Result = string.Empty;
                args.IsPostBack = false;
            }
            else if (args.Result == "no")
            {
                args.AbortPipeline();
            }
            else if (args.Parameters["confirm"] == "1")
            {
                string str;
                Database database = GetDatabase(args);
                Item target = GetTarget(args);
                Item source = GetSource(args, database);
                if (args.Parameters["sortAfter"] == "1")
                {
                    Item item3 = database.Items[args.Parameters["target"]];
                    str = " " + Translate.Text("'{0}' after '{1}'", new object[] { source.Appearance.DisplayName, item3.Appearance.DisplayName });
                }
                else if (args.Parameters["appendAsChild"] != "1")
                {
                    Item item4 = database.Items[args.Parameters["target"]];
                    str = " " + Translate.Text("'{0}' before '{1}'", new object[] { source.Appearance.DisplayName, item4.Appearance.DisplayName });
                }
                else
                {
                    str = " " + Translate.Text("'{0}' to '{1}'", new object[] { source.Appearance.DisplayName, target.Appearance.DisplayName });
                }
                if (args.Parameters["copy"] == "1")
                {
                    Context.ClientPage.ClientResponse.Confirm(Translate.Text("Are you sure you want to copy{0}?", new object[] { str }));
                }
                else
                {
                    Context.ClientPage.ClientResponse.Confirm(Translate.Text("Are you sure you want to move{0}?", new object[] { str }));
                }
                args.WaitForPostBack();
            }
        }

        public void Execute(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            Database database = GetDatabase(args);
            Item source = GetSource(args, database);
            Item target = GetTarget(args);
            if (args.Parameters["copy"] == "1")
            {
                Log.Audit(this, "Copy item: {0} to {1}", new string[] { AuditFormatter.FormatItem(source), AuditFormatter.FormatItem(target) });
                SetSortorder(source.CopyTo(target, ItemUtil.GetCopyOfName(target, source.Name)), args);
            }
            else
            {
                Log.Audit(this, "Drag item: {0} to {1}", new string[] { AuditFormatter.FormatItem(source), AuditFormatter.FormatItem(target) });
                source.MoveTo(target);
                SetSortorder(source, args);
            }
        }

        private static Database GetDatabase(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            Database database = Factory.GetDatabase(args.Parameters["database"]);
            Error.Assert(database != null, "Database \"" + args.Parameters["database"] + "\" not found.");
            return database;
        }

        private static Item GetSource(ClientPipelineArgs args, Database database)
        {
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(database, "database");
            Item item = database.GetItem(args.Parameters["id"], Language.Parse(args.Parameters["language"]));
            Assert.IsNotNull(item, typeof(Item), "ID:{0}", new object[] { args.Parameters["id"] });
            return item;
        }

        /// <summary>
        /// Gets the target.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        private static Item GetTarget(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            Item parent = GetDatabase(args).GetItem(ID.Parse(args.Parameters["target"]), Language.Parse(args.Parameters["language"]));
            Assert.IsNotNull(parent, typeof(Item), "ID:{0}", new object[] { args.Parameters["target"] });
            if (args.Parameters["appendAsChild"] != "1")
            {
                parent = parent.Parent;
                Assert.IsNotNull(parent, typeof(Item), "ID:{0}.Parent", new object[] { args.Parameters["target"] });
            }
            return parent;
        }

        /// <summary>
        /// Determines whether the specified items comes from the same databases.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="item">The item.</param>
        /// <returns>
        /// <c>true</c> if the specified items comes from the same databases; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsSameDatabases(Item target, Item item)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(item, "item");
            return (target.RuntimeSettings.OwnerDatabase.Name == item.RuntimeSettings.OwnerDatabase.Name);
        }

        public void RepairLinks(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (args.Parameters["copy"] != "1")
            {
                Database database = GetDatabase(args);
                Item source = GetSource(args, database);
                JobOptions options = new JobOptions("LinkUpdater", "LinkUpdater", Context.Site.Name, new LinkUpdaterJob(source), "Update")
                {
                    ContextUser = Context.User
                };
                JobManager.Start(options);
            }
        }

        private static int Resort(Item target, DragAction dragAction)
        {
            Assert.ArgumentNotNull(target, "target");
            int num = 0;
            int num2 = 0;
            foreach (Item item in target.Parent.Children)
            {
                // Sitecore Support Fix #93741
                int versionCounter = item.Versions.Count;
                item.Editing.BeginEdit();
                item.Appearance.Sortorder = num2 * 100;
                item.Editing.EndEdit();
                // Sitecore Support Fix #93741
                if (versionCounter == 0)
                    item.Versions.RemoveAll(false);
                if (item.ID == target.ID)
                {
                    num = (dragAction == DragAction.Before) ? ((num2 * 100) - 50) : ((num2 * 100) + 50);
                }
                num2++;
            }
            return num;
        }

        private static void SetItemSortorder(Item item, int sortorder)
        {
            Assert.ArgumentNotNull(item, "item");
            // Sitecore Support Fix #93741
            int versionConuter = item.Versions.Count;
            item.Editing.BeginEdit();
            item.Appearance.Sortorder = sortorder;
            item.Editing.EndEdit();
            // Sitecore Support Fix #93741
            if (versionConuter == 0)
                item.Versions.RemoveAll(false);
        }

        private static void SetSortorder(Item item, ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(args, "args");
            if (args.Parameters["appendAsChild"] != "1")
            {
                Item target = GetDatabase(args).Items[args.Parameters["target"]];
                if (target != null)
                {
                    int sortorder = target.Appearance.Sortorder;
                    if (args.Parameters["sortAfter"] == "1")
                    {
                        Item nextSibling = target.Axes.GetNextSibling();
                        if (nextSibling == null)
                        {
                            sortorder += 100;
                        }
                        else
                        {
                            int num2 = nextSibling.Appearance.Sortorder;
                            if (Math.Abs((int)(num2 - sortorder)) >= 2)
                            {
                                sortorder += (num2 - sortorder) / 2;
                            }
                            else if (target.Parent != null)
                            {
                                sortorder = Resort(target, DragAction.After);
                            }
                        }
                    }
                    else
                    {
                        Item previousSibling = target.Axes.GetPreviousSibling();
                        if (previousSibling == null)
                        {
                            sortorder -= 100;
                        }
                        else
                        {
                            int num3 = previousSibling.Appearance.Sortorder;
                            if (Math.Abs((int)(num3 - sortorder)) >= 2)
                            {
                                sortorder -= (sortorder - num3) / 2;
                            }
                            else if (target.Parent != null)
                            {
                                sortorder = Resort(target, DragAction.Before);
                            }
                        }
                    }
                    SetItemSortorder(item, sortorder);
                }
            }
        }

    }
}