﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.TableOfContents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.DataContracts.Common;
    using Microsoft.DocAsCode.Plugins;

    using YamlDotNet.Core;

    public static class TocHelper
    {
        public static IEnumerable<FileModel> Resolve(ImmutableList<FileModel> models, IHostService host)
        {
            var tocCache = new Dictionary<string, TocItemInfo>(FilePathComparer.OSPlatformSensitiveStringComparer);
            foreach (var model in models)
            {
                tocCache[model.OriginalFileAndType.FullPath] = new TocItemInfo(model.OriginalFileAndType, (TocItemViewModel)model.Content);
            }
            var tocResolver = new TocResolver(host, tocCache);
            foreach (var key in tocCache.Keys.ToList())
            {
                tocCache[key] = tocResolver.Resolve(key);
            }

            foreach (var model in models)
            {
                // If the TOC file is referenced by other TOC, remove it from the collection
                var tocItemInfo = tocCache[model.OriginalFileAndType.FullPath];
                if (!tocItemInfo.IsReferenceToc)
                {
                    model.Content = tocItemInfo.Content;
                    yield return model;
                }
            }
        }

        // TODO: refactor the return value to TocRootViewModel
        public static TocItemViewModel LoadSingleToc(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException(nameof(file));
            }

            var fileType = Utility.GetTocFileType(file);
            try
            {
                if (fileType == TocFileType.Markdown)
                {
                    return new TocItemViewModel
                    {
                        Items = MarkdownTocReader.LoadToc(EnvironmentContext.FileAbstractLayer.ReadAllText(file), file)
                    };
                }
                else if (fileType == TocFileType.Yaml)
                {
                    return LoadYamlToc(file);
                }
            }
            catch (Exception e)
            {
                var message = $"{file} is not a valid TOC File: {e.Message}";
                Logger.LogError(message);
                throw new DocumentException(message, e);
            }

            throw new NotSupportedException($"{file} is not a valid TOC file, supported toc files could be \"{Constants.TableOfContents.MarkdownTocFileName}\" or \"{Constants.TableOfContents.YamlTocFileName}\".");
        }

        public static TocItemViewModel LoadYamlToc(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException(nameof(file));
            }

            try
            {
                return new TocItemViewModel
                {
                    Items = YamlUtility.Deserialize<TocViewModel>(file)
                };
            }
            catch (YamlException exForList)
            {
                try
                {
                    var tocWithMetadata = YamlUtility.Deserialize<TocRootViewModel>(file);
                    return new TocItemViewModel
                    {
                        Items = tocWithMetadata.Items,
                        Metadata = tocWithMetadata.Metadata
                    };
                }
                catch (YamlException exForObject)
                {
                    if (exForList.Start.Index < exForObject.Start.Index)
                    {
                        throw;
                    }
                }
                throw;
            }
        }
    }
}
