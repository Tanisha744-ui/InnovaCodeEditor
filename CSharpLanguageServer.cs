using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using StreamJsonRpc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Workspaces;

namespace InnovaCodeEditor
{
    public class WebSocketStream : Stream
    {
        private readonly WebSocket _webSocket;

        public WebSocketStream(WebSocket webSocket)
        {
            _webSocket = webSocket;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var segment = new ArraySegment<byte>(buffer, offset, count);
            var result = _webSocket.ReceiveAsync(segment, CancellationToken.None).GetAwaiter().GetResult();
            return result.Count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var segment = new ArraySegment<byte>(buffer, offset, count);
            _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    public class CSharpLanguageServer
    {
        private readonly JsonRpc _rpc;
        private readonly AdhocWorkspace _workspace;
        private Project _project;
        private Document _document;

        public CSharpLanguageServer(WebSocket socket)
        {
            var stream = new WebSocketStream(socket);
            _rpc = new JsonRpc(stream, stream, this);
            _workspace = new AdhocWorkspace();
        }

        public async Task StartAsync()
        {
            _rpc.StartListening();
            await Task.Delay(-1);
        }

        [JsonRpcMethod("textDocument/didOpen")]
        public void DidOpen(dynamic param)
        {
            string code = param.textDocument.text;
            _project = _workspace.AddProject("Project", LanguageNames.CSharp)
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            _document = _workspace.AddDocument(_project.Id, "File.cs", SourceText.From(code));
        }

        [JsonRpcMethod("textDocument/didChange")]
        public void DidChange(dynamic param)
        {
            var updated = _document.WithText(SourceText.From(param.contentChanges[0].text));
            _workspace.TryApplyChanges(updated.Project.Solution);
            _document = updated;
        }

        [JsonRpcMethod("textDocument/completion")]
        public async Task<object> Completion(dynamic param)
        {
            int position = param.position.character;
            var completionService = CompletionService.GetService(_document);
            var results = await completionService.GetCompletionsAsync(_document, position);
            if (results == null) return Array.Empty<object>();
            return results.Items.Select(i => new {
                label = i.DisplayText,
                kind = 2, // Method
                insertText = i.DisplayText
            }).ToArray();
        }

        [JsonRpcMethod("textDocument/hover")]
        public async Task<object> Hover(dynamic param)
        {
            var semanticModel = await _document.GetSemanticModelAsync();
            var syntaxRoot = await _document.GetSyntaxRootAsync();
            // Implement symbol lookup and documentation extraction here
            return null;
        }

        [JsonRpcMethod("textDocument/publishDiagnostics")]
        public async Task<object> Diagnostics(dynamic param)
        {
            var diagnostics = (await _document.GetSemanticModelAsync()).GetDiagnostics();
            return diagnostics.Select(d => new {
                range = new {
                    start = new { line = d.Location.GetLineSpan().StartLinePosition.Line, character = d.Location.GetLineSpan().StartLinePosition.Character },
                    end = new { line = d.Location.GetLineSpan().EndLinePosition.Line, character = d.Location.GetLineSpan().EndLinePosition.Character }
                },
                message = d.GetMessage(),
                severity = 1
            }).ToArray();
        }
    }
}
