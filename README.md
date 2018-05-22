# Stratis.Patricia

Merkle Patricia Tries are a tree structure in which each node is stored as a value in a key/value store by its hash. The hash of the root node can thus be used to represent the entire set of data in the trie.

They are particularly useful in smart contract-enabled blockchains as they allow the entire world state to be represented by a single 32-byte hash. Rolling back the internal state to any point in time using patricia tries is trivial. Provided the root node is set to a previously valid state, the internal state will always be able to be resolved via the underlying key/value store.

Usage
-----

Stratis.Patricia is available on [NuGet](https://www.nuget.org/packages/Stratis.Patricia/).

```c#
var key1 = new byte[] {1, 2, 3};
var value1 = new byte[] {4, 5, 6};
var key2 = new byte[] { 7, 8, 9 };
var value2 = new byte[] { 10, 11, 12 };

// Create any data store you like that implements ISource, or use the included MemoryDictionarySource
var underlyingSource = new MemoryDictionarySource();
var trie = new PatriciaTrie(underlyingSource);

// Insert your data into the trie and flush to the underlying data store.
trie.Put(key1, value1);
trie.Put(key2, value2);
trie.Flush();

// Get a 32-byte hash that represents all the data in your trie
byte[] root = trie.GetRootHash();

// So long as you use the same underlying data store, access all the same data
var trie2 = new PatriciaTrie(underlyingSource);
trie2.SetRoot(root);
byte[] equivalentToValue1 = trie2.Get(key1);
```

You can use any data store or hashing algorithm that you like - just implement `ISource` or `IHasher` and pass them into the PatriciaTrie constructor.
