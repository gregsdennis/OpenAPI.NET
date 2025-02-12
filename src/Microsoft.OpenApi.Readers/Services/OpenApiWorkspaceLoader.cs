﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers.Interface;
using Microsoft.OpenApi.Services;

namespace Microsoft.OpenApi.Readers.Services
{
    internal class OpenApiWorkspaceLoader 
    {
        private OpenApiWorkspace _workspace;
        private IStreamLoader _loader;
        private readonly OpenApiReaderSettings _readerSettings;

        public OpenApiWorkspaceLoader(OpenApiWorkspace workspace, IStreamLoader loader, OpenApiReaderSettings readerSettings)
        {
            _workspace = workspace;
            _loader = loader;
            _readerSettings = readerSettings;
        }

        internal async Task LoadAsync(OpenApiReference reference, OpenApiDocument document, CancellationToken cancellationToken)
        {
            _workspace.AddDocument(reference.ExternalResource, document);
            document.Workspace = _workspace;

            // Collect remote references by walking document
            var referenceCollector = new OpenApiRemoteReferenceCollector(document);
            var collectorWalker = new OpenApiWalker(referenceCollector);
            collectorWalker.Walk(document);

            var reader = new OpenApiStreamReader(_readerSettings);

            // Walk references
            foreach (var item in referenceCollector.References)
            {
                // If not already in workspace, load it and process references
                if (!_workspace.Contains(item.ExternalResource))
                {
                    var input = await _loader.LoadAsync(new Uri(item.ExternalResource, UriKind.RelativeOrAbsolute));
                    var result = await reader.ReadAsync(input, cancellationToken); // TODO merge diagnostics
                    await LoadAsync(item, result.OpenApiDocument, cancellationToken);
                }
            }
        }
    }
}
