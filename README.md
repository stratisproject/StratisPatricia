# Stratis.Patricia

Merkle Patricia Tries are a tree structure in which each node is stored as a value in a key/value store by its hash. The hash of the root node can thus be used to represent the entire set of data in the trie.

They are particularly useful in smart contract-enabled blockchains as they allow the entire world state to be represented by a single 32-byte hash. Rolling back the internal state to any point in time using patricia tries is trivial. Provided the root node is set to a previously valid state, the internal state will always be able to be resolved via the underlying key/value store.

Usage
-----

Stratis.Patricia is available on [NuGet](https://www.nuget.org/packages/Stratis.Patricia/).

```
var trie = new PatriciaTrie();
```
