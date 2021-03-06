﻿using InfoCaster.Umbraco.UrlTracker.Extensions;
using InfoCaster.Umbraco.UrlTracker.Helpers;
using InfoCaster.Umbraco.UrlTracker.Models;
using InfoCaster.Umbraco.UrlTracker.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using umbraco;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;
using umbraco.DataLayer;
using umbraco.NodeFactory;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web.UI.Pages;

namespace InfoCaster.Umbraco.UrlTracker
{
    public class UrlTrackerApplicationEventHandler : ApplicationEventHandler
    {
        protected ClientTools ClientTools
        {
            get
            {
                Page page = HttpContext.Current.CurrentHandler as Page;
                if (page != null)
                    return new ClientTools(page);
                return null;
            }
        }

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarted(umbracoApplication, applicationContext);

            if (!UrlTrackerSettings.IsDisabled && !UrlTrackerSettings.IsTrackingDisabled)
            {
                UrlTrackerRepository.ReloadForcedRedirectsCache();

                ContentService.Moving += ContentService_Moving;
                ContentService.Publishing += ContentService_Publishing;
                ContentService.Deleting += ContentService_Deleting;
                content.BeforeClearDocumentCache += content_BeforeClearDocumentCache;
                Domain.AfterDelete += Domain_AfterDelete;
                Domain.AfterSave += Domain_AfterSave;
                Domain.New += Domain_New;
            }
        }

        void ContentService_Deleting(IContentService sender, DeleteEventArgs<IContent> e)
        {
            foreach (IContent content in e.DeletedEntities)
            {
#if !DEBUG
                try
#endif
                {
                    UrlTrackerRepository.DeleteUrlTrackerEntriesByNodeId(content.Id);
                }
#if !DEBUG
                catch (Exception ex)
                {
                    ex.LogException();
                }
#endif
            }
        }

        void ContentService_Publishing(IPublishingStrategy sender, PublishEventArgs<IContent> e)
        {
            // When content is renamed or 'umbracoUrlName' property value is added/updated
            foreach (IContent content in e.PublishedEntities)
            {
#if !DEBUG
                try
#endif
                {
                    Node node = new Node(content.Id);
                    if (node.Name != content.Name && !string.IsNullOrEmpty(node.Name)) // If name is null, it's a new document
                    {
                        // Rename occurred
                        UrlTrackerRepository.AddUrlMapping(content, node.GetDomainRootNode().Id, node.NiceUrl, AutoTrackingTypes.Renamed);

                        if (ClientTools != null)
                            ClientTools.ChangeContentFrameUrl(string.Concat("/umbraco/editContent.aspx?id=", content.Id));
                    }
                    if (content.HasProperty("umbracoUrlName"))
                    {
                        string contentUmbracoUrlNameValue = content.GetValue("umbracoUrlName") != null ? content.GetValue("umbracoUrlName").ToString() : string.Empty;
                        string nodeUmbracoUrlNameValue = node.GetProperty("umbracoUrlName") != null ? node.GetProperty("umbracoUrlName").Value : string.Empty;
                        if (contentUmbracoUrlNameValue != nodeUmbracoUrlNameValue)
                        {
                            // 'umbracoUrlName' property value added/changed
                            UrlTrackerRepository.AddUrlMapping(content, node.GetDomainRootNode().Id, node.NiceUrl, AutoTrackingTypes.UrlOverwritten);

                            if (ClientTools != null)
                                ClientTools.ChangeContentFrameUrl(string.Concat("/umbraco/editContent.aspx?id=", content.Id));
                        }
                    }
                }
#if !DEBUG
                catch (Exception ex)
                {
                    ex.LogException();
                }
#endif
            }
        }

        void ContentService_Moving(IContentService sender, MoveEventArgs<IContent> e)
        {
            IContent content = e.Entity;
#if !DEBUG
            try
#endif
            {
                if (content != null)
                {
                    Node node = new Node(content.Id);

                    if (node != null && !string.IsNullOrEmpty(node.NiceUrl) && !content.Path.StartsWith("-1,-20")) // -1,-20 == Recycle bin | Not moved to recycle bin
                        UrlTrackerRepository.AddUrlMapping(content, node.GetDomainRootNode().Id, node.NiceUrl, AutoTrackingTypes.Moved);
                }
            }
#if !DEBUG
            catch (Exception ex)
            {
                ex.LogException();
            }
#endif
        }

        void content_BeforeClearDocumentCache(Document doc, DocumentCacheEventArgs e)
        {
#if !DEBUG
            try
#endif
            {
                UrlTrackerRepository.AddGoneEntryByNodeId(doc.Id);
            }
#if !DEBUG
            catch (Exception ex)
            {
                ex.LogException();
            }
#endif
        }

        void Domain_New(Domain sender, NewEventArgs e)
        {
            UmbracoHelper.ClearDomains();
        }

        void Domain_AfterSave(Domain sender, SaveEventArgs e)
        {
            UmbracoHelper.ClearDomains();
        }

        void Domain_AfterDelete(Domain sender, umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            UmbracoHelper.ClearDomains();
        }
    }
}