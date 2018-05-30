using Nethereum.RLP;

namespace Stratis.Patricia
{
    /// <summary>
    /// A merkle patricia trie implementation. Stores data in a trie and key/mapping structure
    /// in such a way that all of the data can be represented by a 32-bit hash.
    /// Full definition at: https://github.com/ethereum/wiki/wiki/Patricia-Tree
    /// </summary>
    public class PatriciaTrie : IPatriciaTrie
    {
        internal static readonly byte[] EmptyByteArray = new byte[0];
        internal static readonly byte[] EmptyElementRlp = RLP.EncodeElement(EmptyByteArray);
        private readonly byte[] emptyTrieHash;

        /// <summary>
        /// The key/value store used to store nodes and data by their hashes.
        /// </summary>
        internal ISource<byte[], byte[]> TrieKvStore { get; }

        /// <summary>
        /// Used to hash nodes and values.
        /// </summary>
        internal IHasher Hasher { get; }


        /// <summary>
        /// The root of the trie.
        /// </summary>
        private Node root;

        public PatriciaTrie() : this(null, new MemoryDictionarySource(), new Keccak256Hasher()) { }

        public PatriciaTrie(byte[] root) : this(root, new MemoryDictionarySource(), new Keccak256Hasher()) { }

        public PatriciaTrie(ISource<byte[],byte[]> trieKvStore) : this(null, trieKvStore, new Keccak256Hasher()) { }

        public PatriciaTrie(byte[] root, ISource<byte[], byte[]> trieKvStore) : this(root, trieKvStore, new Keccak256Hasher()) { }

        public PatriciaTrie(byte[] root, ISource<byte[],byte[]> trieKvStore, IHasher hasher)
        {
            // Set this first because SetRootHash does check to see if root given is an empty value!
            this.emptyTrieHash = hasher.Hash(EmptyElementRlp);

            this.TrieKvStore = trieKvStore;
            this.Hasher = hasher;
            SetRootHash(root);
        }

        /// <inheritdoc />
        public void SetRootHash(byte[] hash)
        {
            if (hash != null && !new ByteArrayComparer().Equals(hash, this.emptyTrieHash))
            {
                this.root = new Node(hash, this);
            }
            else
            {
                this.root = null;
            }
        }

        /// <inheritdoc />
        public byte[] GetRootHash()
        {
            Encode();
            return this.root != null ? this.root.Hash : this.emptyTrieHash;
        }

        /// <inheritdoc />
        public byte[] Get(byte[] key)
        {
            if (!HasRoot())
                return null;
            Key k = Key.FromNormal(key);
            return Get(this.root, k);
        }

        /// <inheritdoc />
        public void Put(byte[] key, byte[] value)
        {
            Key k = Key.FromNormal(key);
            if (this.root == null)
            {
                if (value != null && value.Length > 0)
                {
                    this.root = new Node(k, value, this);
                }
            }
            else
            {
                if (value == null || value.Length == 0)
                {
                    this.root = Delete(this.root, k);
                }
                else
                {
                    this.root = Insert(this.root, k, value);
                }
            }
        }

        /// <inheritdoc />
        public void Delete(byte[] key)
        {
            Key k = Key.FromNormal(key);
            if (this.root != null)
            {
                this.root = Delete(this.root, k);
            }
        }

        /// <inheritdoc />
        public bool Flush()
        {
            if (this.root != null && this.root.Dirty)
            {
                // persist all dirty Nodes to underlying Source
                Encode();
                // release all Trie Node instances for GC
                this.root = new Node(this.root.Hash, this);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool HasRoot()
        {
            return this.root != null && this.root.ResolveCheck();
        }

        private byte[] Get(Node node, Key key)
        {
            if (node == null)
                return null;

            NodeType type = node.NodeType;

            if (type == NodeType.BranchNode)
            {
                if (key.IsEmpty)
                    return node.BranchNodeGetValue();

                Node childNode = node.BranchNodeGetChild(key.GetHex(0));
                return Get(childNode, key.Shift(1));
            }
            else
            {
                Key k1 = key.MatchAndShift(node.KvNodeGetKey());
                if (k1 == null)
                    return null;
                if (type == NodeType.KeyValueNodeValue)
                {
                    return k1.IsEmpty ? node.KvNodeGetValue() : null;
                }
                else
                {
                    return Get(node.KvNodeGetChildNode(), k1);
                }
            }
        }

        private Node Insert(Node node, Key key, object toInsert)
        {
            NodeType type = node.NodeType;
            if (type == NodeType.BranchNode)
            {
                if (key.IsEmpty) return node.BranchNodeSetValue((byte[])toInsert);
                Node childNode = node.BranchNodeGetChild(key.GetHex(0));
                if (childNode != null)
                {
                    return node.BranchNodeSetChild(key.GetHex(0), Insert(childNode, key.Shift(1), toInsert));
                }
                else
                {
                    Key childKey = key.Shift(1);
                    Node newChildNode;
                    if (!childKey.IsEmpty)
                    {
                        newChildNode = new Node(childKey, toInsert, this);
                    }
                    else
                    {
                        newChildNode = toInsert is Node nodeToInsert 
                            ? nodeToInsert 
                            : new Node(childKey, toInsert, this);
                    }
                    return node.BranchNodeSetChild(key.GetHex(0), newChildNode);
                }
            }
            else
            {
                Key commonPrefix = key.GetCommonPrefix(node.KvNodeGetKey());
                if (commonPrefix.IsEmpty)
                {
                    Node newBranchNode = new Node(this);
                    Insert(newBranchNode, node.KvNodeGetKey(), node.KvNodeGetValueOrNode());
                    Insert(newBranchNode, key, toInsert);
                    node.Dispose();
                    return newBranchNode;
                }
                else if (commonPrefix.Equals(key))
                {
                    return node.KvNodeSetValueOrNode(toInsert);
                }
                else if (commonPrefix.Equals(node.KvNodeGetKey()))
                {
                    Insert(node.KvNodeGetChildNode(), key.Shift(commonPrefix.Length), toInsert);
                    return node.Invalidate();
                }
                else
                {
                    Node newBranchNode = new Node(this);
                    Node newKvNode = new Node(commonPrefix, newBranchNode, this);
                    // TODO can be optimized
                    Insert(newKvNode, node.KvNodeGetKey(), node.KvNodeGetValueOrNode());
                    Insert(newKvNode, key, toInsert);
                    node.Dispose();
                    return newKvNode;
                }
            }
        }

        private Node Delete(Node node, Key key)
        {
            NodeType type = node.NodeType;
            Node newKvNode;
            if (type == NodeType.BranchNode)
            {
                if (key.IsEmpty)
                {
                    node.BranchNodeSetValue(null);
                }
                else
                {
                    int idx = key.GetHex(0);
                    Node child = node.BranchNodeGetChild(idx);
                    if (child == null)
                        return node; // no key found

                    Node newNode = Delete(child, key.Shift(1));
                    node.BranchNodeSetChild(idx, newNode);
                    if (newNode != null)
                        return node; // number of children didn't decrease
                }

                // child node or value was deleted
                // lets see if we can compact
                int compactIdx = node.BranchNodeCompactIndex();
                if (compactIdx < 0)
                    return node; // no compaction is required

                // only value or a single child left - compact branch Node to kvNode
                node.Dispose();
                if (compactIdx == 16)
                { // only value left
                    return new Node(Key.Empty(), node.BranchNodeGetValue(), this);
                }
                else
                { // only single child left
                    newKvNode = new Node(Key.SingleHex(compactIdx), node.BranchNodeGetChild(compactIdx), this);
                }
            }
            else
            { // node - kvNode
                Key k1 = key.MatchAndShift(node.KvNodeGetKey());
                if (k1 == null)
                {
                    // no key found
                    return node;
                }
                else if (type == NodeType.KeyValueNodeValue)
                {
                    if (k1.IsEmpty)
                    {
                        // delete this kvNode
                        node.Dispose();
                        return null;
                    }
                    else
                    {
                        // else no key found
                        return node;
                    }
                }
                else
                {
                    Node newChild = Delete(node.KvNodeGetChildNode(), k1);
                    if (newChild == null)
                        throw new PatriciaTreeResolutionException("New node failed instantiation after deletion.");
                    newKvNode = node.KvNodeSetValueOrNode(newChild);
                }
            }

            // if we get here a new kvNode was created, now need to check
            // if it should be compacted with child kvNode
            Node nChild = newKvNode.KvNodeGetChildNode();
            if (nChild.NodeType != NodeType.BranchNode)
            {
                // two kvNodes should be compacted into a single one
                Key newKey = newKvNode.KvNodeGetKey().Concat(nChild.KvNodeGetKey());
                Node newNode = new Node(newKey, nChild.KvNodeGetValueOrNode(), this);
                nChild.Dispose();
                return newNode;
            }
            else
            {
                // no compaction needed
                return newKvNode;
            }
        }

        private void Encode()
        {
            this.root?.Encode();
        }
    }
}
