namespace Nolock.social.OCRservices.Utils;

public class MimeTypeTrie
{
    private class TrieNode
    {
        public Dictionary<byte, TrieNode> Children { get; } = new();
        public string? MimeType { get; set; }
    }

    private readonly TrieNode _root = new();

    public void Add(byte[] signature, string mimeType)
    {
        var current = _root;
        foreach (var b in signature)
        {
            if (!current.Children.TryGetValue(b, out var child))
            {
                child = new TrieNode();
                current.Children[b] = child;
            }
            current = child;
        }
        
        if (current.MimeType != null && current.MimeType != mimeType)
        {
            throw new InvalidOperationException($"Attempt to override existing MIME type '{current.MimeType}' with '{mimeType}' for the same signature.");
        }
        
        current.MimeType = mimeType;
    }

    public string? Search(byte[] data)
    {
        var current = _root;
        string? lastFoundMimeType = null;
        
        foreach (var item in data)
        {
            if (!current.Children.TryGetValue(item, out var child))
            {
                break;
            }
            
            current = child;
            
            if (current.MimeType != null)
            {
                lastFoundMimeType = current.MimeType;
            }
        }
        
        return lastFoundMimeType;
    }

    public IEnumerable<string> GetAllMimeTypes()
    {
        var stack = new Stack<TrieNode>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            
            if (node.MimeType != null)
            {
                yield return node.MimeType;
            }

            foreach (var child in node.Children.Values)
            {
                stack.Push(child);
            }
        }
    }
}