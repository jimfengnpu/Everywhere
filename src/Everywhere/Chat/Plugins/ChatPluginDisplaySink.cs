using System.Collections.ObjectModel;
using LiveMarkdown.Avalonia;
using MessagePack;
using MessagePack.Formatters;

namespace Everywhere.Chat.Plugins;

[MessagePackObject(SuppressSourceGeneration = true)]
[MessagePackFormatter(typeof(ChatPluginDisplaySinkFormatter))]
public sealed class ChatPluginDisplaySink : ObservableCollection<ChatPluginDisplayBlock>, IChatPluginDisplaySink
{
    public void AppendBlock(ChatPluginDisplayBlock block)
    {
        Add(block);
    }

    public void AppendBlocks(IEnumerable<ChatPluginDisplayBlock> blocks)
    {
        this.AddRange(blocks);
    }

    public IChatPluginDisplaySink AppendContainer()
    {
        var groupBlock = new ChatPluginContainerDisplayBlock();
        Add(groupBlock);
        return groupBlock.Children;
    }

    public void AppendText(string text, string? fontFamily = null)
    {
        Add(new ChatPluginTextDisplayBlock(text));
    }

    public void AppendDynamicResourceKey(DynamicResourceKeyBase resourceKey)
    {
        Add(new ChatPluginDynamicResourceKeyDisplayBlock(resourceKey));
    }

    public ObservableStringBuilder AppendMarkdown()
    {
        var markdownBlock = new ChatPluginMarkdownDisplayBlock();
        Add(markdownBlock);
        return markdownBlock.MarkdownBuilder;
    }

    public IProgress<double> AppendProgress(DynamicResourceKeyBase headerKey)
    {
        var progressBlock = new ChatPluginProgressDisplayBlock(headerKey);
        Add(progressBlock);
        return progressBlock.ProgressReporter;
    }

    public void AppendFileReferences(params IReadOnlyList<ChatPluginFileReference> references)
    {
        Add(new ChatPluginFileReferencesDisplayBlock(references));
    }

    public void AppendFileDifference(TextDifference difference, string originalText)
    {
        Add(new ChatPluginFileDifferenceDisplayBlock(difference, originalText));
    }

    public void AppendUrls(IReadOnlyList<ChatPluginUrl> urls)
    {
        Add(new ChatPluginUrlsDisplayBlock(urls));
    }

    public void AppendSeparator(double thickness = 1)
    {
        Add(new ChatPluginSeparatorDisplayBlock(thickness));
    }

    public void AppendCodeBlock(string code, string? language = null)
    {
        Add(new ChatPluginCodeBlockDisplayBlock(code, language));
    }
}

public sealed class ChatPluginDisplaySinkFormatter : IMessagePackFormatter<ChatPluginDisplaySink?>
{
    public void Serialize(ref MessagePackWriter writer, ChatPluginDisplaySink? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
        }
        else
        {
            var formatter = options.Resolver.GetFormatterWithVerify<ChatPluginDisplayBlock>();
            var count = value.Count;
            writer.WriteArrayHeader(count);
            for (var i = 0; i < count; i++)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                formatter.Serialize(ref writer, value[i], options);
            }
        }
    }

    public ChatPluginDisplaySink? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var formatter = options.Resolver.GetFormatterWithVerify<ChatPluginDisplayBlock?>();
        var count = reader.ReadArrayHeader();
        var result = new ChatPluginDisplaySink();
        options.Security.DepthStep(ref reader);
        try
        {
            for (var i = 0; i < count; i++)
            {
                reader.CancellationToken.ThrowIfCancellationRequested();
                if (formatter.Deserialize(ref reader, options) is not { } item) continue;
                result.Add(item);
            }
        }
        finally
        {
            reader.Depth--;
        }

        return result;
    }
}