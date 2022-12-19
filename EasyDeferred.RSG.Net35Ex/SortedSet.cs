
//
// SortedSet.cs
//
// Authors:
// Jb Evain <jbevain@novell.com>
//
// Copyright (C) 2010 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Diagnostics;

// SortedSet is basically implemented as a reduction of SortedDictionary<K, V>

//#if NET_4_0

namespace EasyDeferred.RSG//System.Collections
{
   

    [Serializable]
    [DebuggerDisplay("Count={Count}")]
    //[DebuggerTypeProxy(typeof(CollectionDebuggerView))]
    public class SortedSet<T> : ISet<T>, ICollection, ISerializable, IDeserializationCallback
    {
        class Node : RBTree.Node
        {

            public T item;

            public Node(T item) {
                this.item = item;
            }

            public override void SwapValue(RBTree.Node other) {
                var o = (Node)other;
                var i = this.item;
                this.item = o.item;
                o.item = i;
            }
        }

        class NodeHelper : RBTree.INodeHelper<T>
        {

            static NodeHelper Default = new NodeHelper(Comparer<T>.Default);

            public IComparer<T> comparer;

            public int Compare(T item, RBTree.Node node) {
                return comparer.Compare(item, ((Node)node).item);
            }

            public RBTree.Node CreateNode(T item) {
                return new Node(item);
            }

            NodeHelper(IComparer<T> comparer) {
                this.comparer = comparer;
            }

            public static NodeHelper GetHelper(IComparer<T> comparer) {
                if (comparer == null || comparer == Comparer<T>.Default)
                    return Default;

                return new NodeHelper(comparer);
            }
        }

        RBTree tree;
        NodeHelper helper;
        SerializationInfo si;

        public SortedSet()
            : this(Comparer<T>.Default) {
        }

        public SortedSet(IEnumerable<T> collection)
            : this(collection, Comparer<T>.Default) {
        }

        public SortedSet(IEnumerable<T> collection, IComparer<T> comparer)
            : this(comparer) {
            if (collection == null)
                throw new ArgumentNullException("collection");

            foreach (var item in collection)
                Add(item);
        }

        public SortedSet(IComparer<T> comparer) {
            this.helper = NodeHelper.GetHelper(comparer);
            this.tree = new RBTree(this.helper);
        }

        protected SortedSet(SerializationInfo info, StreamingContext context) {
            this.si = info;
        }

        public IComparer<T> Comparer {
            get { return helper.comparer; }
        }

        public int Count {
            get { return GetCount(); }
        }

        public T Max {
            get { return GetMax(); }
        }

        public T Min {
            get { return GetMin(); }
        }

        internal virtual T GetMax() {
            if (tree.Count == 0)
                return default(T);

            return GetItem(tree.Count - 1);
        }

        internal virtual T GetMin() {
            if (tree.Count == 0)
                return default(T);

            return GetItem(0);
        }

        internal virtual int GetCount() {
            return tree.Count;
        }

        T GetItem(int index) {
            return ((Node)tree[index]).item;
        }

        public bool Add(T item) {
            return TryAdd(item);
        }

        internal virtual bool TryAdd(T item) {
            var node = new Node(item);
            return tree.Intern(item, node) == node;
        }

        public virtual void Clear() {
            tree.Clear();
        }

        public virtual bool Contains(T item) {
            return tree.Lookup(item) != null;
        }

        public void CopyTo(T[] array) {
            CopyTo(array, 0, Count);
        }

        public void CopyTo(T[] array, int index) {
            CopyTo(array, index, Count);
        }

        public void CopyTo(T[] array, int index, int count) {
            if (array == null)
                throw new ArgumentNullException("array");
            if (index < 0)
                throw new ArgumentOutOfRangeException("index");
            if (index > array.Length)
                throw new ArgumentException("index larger than largest valid index of array");
            if (array.Length - index < count)
                throw new ArgumentException("destination array cannot hold the requested elements");

            foreach (Node node in tree) {
                if (count-- == 0)
                    break;

                array[index++] = node.item;
            }
        }

        public bool Remove(T item) {
            return TryRemove(item);
        }

        internal virtual bool TryRemove(T item) {
            return tree.Remove(item) != null;
        }

        public int RemoveWhere(Predicate<T> match) {
            var array = ToArray();

            int count = 0;
            foreach (var item in array) {
                if (!match(item))
                    continue;

                Remove(item);
                count++;
            }

            return count;
        }

        public IEnumerable<T> Reverse() {
            for (int i = tree.Count - 1; i >= 0; i--)
                yield return GetItem(i);
        }

        T[] ToArray() {
            var array = new T[this.Count];
            CopyTo(array);
            return array;
        }

        public Enumerator GetEnumerator() {
            return TryGetEnumerator();
        }

        internal virtual Enumerator TryGetEnumerator() {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public static IEqualityComparer<SortedSet<T>> CreateSetComparer() {
            return CreateSetComparer(EqualityComparer<T>.Default);
        }

        //[MonoTODO]
        public static IEqualityComparer<SortedSet<T>> CreateSetComparer(IEqualityComparer<T> memberEqualityComparer) {
            throw new NotImplementedException();
        }

        //[MonoTODO]
        protected virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            throw new NotImplementedException();
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            GetObjectData(info, context);
        }

        //[MonoTODO]
        protected virtual void OnDeserialization(object sender) {
            if (si == null)
                return;

            throw new NotImplementedException();
        }

        void IDeserializationCallback.OnDeserialization(object sender) {
            OnDeserialization(sender);
        }

        //[MonoLimitation("Isn't O(n) when other is SortedSet<T>")]
        public void ExceptWith(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");
            foreach (T item in other)
                Remove(item);
        }

        public virtual SortedSet<T> GetViewBetween(T lowerValue, T upperValue) {
            if (Comparer.Compare(lowerValue, upperValue) > 0)
                throw new ArgumentException("The lowerValue is bigger than upperValue");

            return new SortedSubSet(this, lowerValue, upperValue);
        }

        //[MonoLimitation("Isn't O(n) when other is SortedSet<T>")]
        public virtual void IntersectWith(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            RBTree newtree = new RBTree(helper);
            foreach (T item in other) {
                var node = tree.Remove(item);
                if (node != null)
                    newtree.Intern(item, node);
            }
            tree = newtree;
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            if (Count == 0) {
                foreach (T item in other)
                    return true; // this idiom means: if 'other' is non-empty, return true
                return false;
            }

            return is_subset_of(other, true);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            if (Count == 0)
                return false;

            return is_superset_of(other, true);
        }

        public bool IsSubsetOf(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            if (Count == 0)
                return true;

            return is_subset_of(other, false);
        }

        public bool IsSupersetOf(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            if (Count == 0) {
                foreach (T item in other)
                    return false; // this idiom means: if 'other' is non-empty, return false
                return true;
            }

            return is_superset_of(other, false);
        }

        // Precondition: Count != 0, other != null
        bool is_subset_of(IEnumerable<T> other, bool proper) {
            SortedSet<T> that = nodups(other);

            if (Count > that.Count)
                return false;
            // Count != 0 && Count <= that.Count => that.Count != 0
            if (proper && Count == that.Count)
                return false;
            return that.covers(this);
        }

        // Precondition: Count != 0, other != null
        bool is_superset_of(IEnumerable<T> other, bool proper) {
            SortedSet<T> that = nodups(other);

            if (that.Count == 0)
                return true;
            if (Count < that.Count)
                return false;
            if (proper && Count == that.Count)
                return false;
            return this.covers(that);
        }

        public bool Overlaps(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            if (Count == 0)
                return false;

            // Don't use 'nodups' here. Only optimize the SortedSet<T> case
            SortedSet<T> that = other as SortedSet<T>;
            if (that != null && that.Comparer != Comparer)
                that = null;

            if (that != null)
                return that.Count != 0 && overlaps(that);

            foreach (T item in other)
                if (Contains(item))
                    return true;
            return false;
        }

        public bool SetEquals(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            if (Count == 0) {
                foreach (T item in other)
                    return false;
                return true;
            }

            SortedSet<T> that = nodups(other);

            if (Count != that.Count)
                return false;

            using (var t = that.GetEnumerator()) {
                foreach (T item in this) {
                    if (!t.MoveNext())
                        throw new SystemException("count wrong somewhere: this longer than that");
                    if (Comparer.Compare(item, t.Current) != 0)
                        return false;
                }
                if (t.MoveNext())
                    throw new SystemException("count wrong somewhere: this shorter than that");
                return true;
            }
        }

        SortedSet<T> nodups(IEnumerable<T> other) {
            SortedSet<T> that = other as SortedSet<T>;
            if (that != null && that.Comparer == Comparer)
                return that;
            return new SortedSet<T>(other, Comparer);
        }

        bool covers(SortedSet<T> that) {
            using (var t = that.GetEnumerator()) {
                if (!t.MoveNext())
                    return true;
                foreach (T item in this) {
                    int cmp = Comparer.Compare(item, t.Current);
                    if (cmp > 0)
                        return false;
                    if (cmp == 0 && !t.MoveNext())
                        return true;
                }
                return false;
            }
        }

        bool overlaps(SortedSet<T> that) {
            using (var t = that.GetEnumerator()) {
                if (!t.MoveNext())
                    return false;
                foreach (T item in this) {
                    int cmp;
                    while ((cmp = Comparer.Compare(item, t.Current)) > 0) {
                        if (!t.MoveNext())
                            return false;
                    }
                    if (cmp == 0)
                        return true;
                }
                return false;
            }
        }

        //[MonoLimitation("Isn't O(n) when other is SortedSet<T>")]
        public void SymmetricExceptWith(IEnumerable<T> other) {
            SortedSet<T> that_minus_this = new SortedSet<T>(Comparer);

            // compute this - that and that - this in parallel
            foreach (T item in nodups(other))
                if (!Remove(item))
                    that_minus_this.Add(item);

            UnionWith(that_minus_this);
        }

        //[MonoLimitation("Isn't O(n) when other is SortedSet<T>")]
        public void UnionWith(IEnumerable<T> other) {
            CheckArgumentNotNull(other, "other");

            foreach (T item in other)
                Add(item);
        }

        static void CheckArgumentNotNull(object arg, string name) {
            if (arg == null)
                throw new ArgumentNullException(name);
        }

        void ICollection<T>.Add(T item) {
            Add(item);
        }

        bool ICollection<T>.IsReadOnly {
            get { return false; }
        }

        void ICollection.CopyTo(Array array, int index) {
            if (Count == 0)
                return;
            if (array == null)
                throw new ArgumentNullException("array");
            if (index < 0 || array.Length <= index)
                throw new ArgumentOutOfRangeException("index");
            if (array.Length - index < Count)
                throw new ArgumentException();

            foreach (Node node in tree)
                array.SetValue(node.item, index++);
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        // TODO:Is this correct? If this is wrong,please fix.
        object ICollection.SyncRoot {
            get { return this; }
        }

        [Serializable]
        public struct Enumerator : IEnumerator<T>, IDisposable
        {

            RBTree.NodeEnumerator host;

            IComparer<T> comparer;

            T current;
            T upper;

            internal Enumerator(SortedSet<T> set)
                : this() {
                host = set.tree.GetEnumerator();
            }

            internal Enumerator(SortedSet<T> set, T lower, T upper)
                : this() {
                host = set.tree.GetSuffixEnumerator(lower);
                comparer = set.Comparer;
                this.upper = upper;
            }

            public T Current {
                get { return current; }
            }

            object IEnumerator.Current {
                get {
                    host.check_current();
                    return ((Node)host.Current).item;
                }
            }

            public bool MoveNext() {
                if (!host.MoveNext())
                    return false;

                current = ((Node)host.Current).item;
                return comparer == null || comparer.Compare(upper, current) >= 0;
            }

            public void Dispose() {
                host.Dispose();
            }

            void IEnumerator.Reset() {
                host.Reset();
            }
        }

        [Serializable]
        sealed class SortedSubSet : SortedSet<T>, IEnumerable<T>, IEnumerable
        {

            SortedSet<T> set;
            T lower;
            T upper;

            public SortedSubSet(SortedSet<T> set, T lower, T upper)
                : base(set.Comparer) {
                this.set = set;
                this.lower = lower;
                this.upper = upper;

            }

            internal override T GetMin() {
                RBTree.Node lb = null, ub = null;
                set.tree.Bound(lower, ref lb, ref ub);

                if (ub == null || set.helper.Compare(upper, ub) < 0)
                    return default(T);

                return ((Node)ub).item;
            }

            internal override T GetMax() {
                RBTree.Node lb = null, ub = null;
                set.tree.Bound(upper, ref lb, ref ub);

                if (lb == null || set.helper.Compare(lower, lb) > 0)
                    return default(T);

                return ((Node)lb).item;
            }

            internal override int GetCount() {
                int count = 0;
                using (var e = set.tree.GetSuffixEnumerator(lower)) {
                    while (e.MoveNext() && set.helper.Compare(upper, e.Current) >= 0)
                        ++count;
                }
                return count;
            }

            internal override bool TryAdd(T item) {
                if (!InRange(item))
                    throw new ArgumentOutOfRangeException("item");

                return set.TryAdd(item);
            }

            internal override bool TryRemove(T item) {
                if (!InRange(item))
                    return false;

                return set.TryRemove(item);
            }

            public override bool Contains(T item) {
                if (!InRange(item))
                    return false;

                return set.Contains(item);
            }

            public override void Clear() {
                set.RemoveWhere(InRange);
            }

            bool InRange(T item) {
                return Comparer.Compare(item, lower) >= 0
                        && Comparer.Compare(item, upper) <= 0;
            }

            public override SortedSet<T> GetViewBetween(T lowerValue, T upperValue) {
                if (Comparer.Compare(lowerValue, upperValue) > 0)
                    throw new ArgumentException("The lowerValue is bigger than upperValue");
                if (!InRange(lowerValue))
                    throw new ArgumentOutOfRangeException("lowerValue");
                if (!InRange(upperValue))
                    throw new ArgumentOutOfRangeException("upperValue");

                return new SortedSubSet(set, lowerValue, upperValue);
            }

            internal override Enumerator TryGetEnumerator() {
                return new Enumerator(set, lower, upper);
            }

            public override void IntersectWith(IEnumerable<T> other) {
                CheckArgumentNotNull(other, "other");

                var slice = new SortedSet<T>(this);
                slice.IntersectWith(other);

                Clear();
                set.UnionWith(slice);
            }
        }
    }

    [Serializable]
    internal class RBTree : IEnumerable, IEnumerable<RBTree.Node>
    {
        public interface INodeHelper<T>
        {
            int Compare(T key, Node node);
            Node CreateNode(T key);
        }

        public abstract class Node
        {
            public Node left, right;
            uint size_black;

            const uint black_mask = 1;
            const int black_shift = 1;
            public bool IsBlack {
                get { return (size_black & black_mask) == black_mask; }
                set { size_black = value ? (size_black | black_mask) : (size_black & ~black_mask); }
            }

            public uint Size {
                get { return size_black >> black_shift; }
                set { size_black = (value << black_shift) | (size_black & black_mask); }
            }

            public uint FixSize() {
                Size = 1;
                if (left != null)
                    Size += left.Size;
                if (right != null)
                    Size += right.Size;
                return Size;
            }

            public Node() {
                size_black = 2; // Size == 1, IsBlack = false
            }

            public abstract void SwapValue(Node other);

#if TEST
                        public int VerifyInvariants ()
                        {
                                int black_depth_l = 0;
                                int black_depth_r = 0;
                                uint size = 1;
                                bool child_is_red = false;
                                if (left != null) {
                                        black_depth_l = left.VerifyInvariants ();
                                        size += left.Size;
                                        child_is_red |= !left.IsBlack;
                                }

                                if (right != null) {
                                        black_depth_r = right.VerifyInvariants ();
                                        size += right.Size;
                                        child_is_red |= !right.IsBlack;
                                }

                                if (black_depth_l != black_depth_r)
                                        throw new SystemException ("Internal error: black depth mismatch");

                                if (!IsBlack && child_is_red)
                                        throw new SystemException ("Internal error: red-red conflict");
                                if (Size != size)
                                        throw new SystemException ("Internal error: metadata error");

                                return black_depth_l + (IsBlack ? 1 : 0);
                        }

                        public abstract void Dump (string indent);
#endif
        }

        Node root;
        object hlp;
        uint version;

#if ONE_MEMBER_CACHE
#if TARGET_JVM
                static readonly LocalDataStoreSlot _cachedPathStore = System.Threading.Thread.AllocateDataSlot ();

                static List<Node> cached_path {
                        get { return (List<Node>) System.Threading.Thread.GetData (_cachedPathStore); }
                        set { System.Threading.Thread.SetData (_cachedPathStore, value); }
                }
#else
        [ThreadStatic]
        static List<Node> cached_path;
#endif

        static List<Node> alloc_path() {
            if (cached_path == null)
                return new List<Node>();

            List<Node> path = cached_path;
            cached_path = null;
            return path;
        }

        static void release_path(List<Node> path) {
            if (cached_path == null || cached_path.Capacity < path.Capacity) {
                path.Clear();
                cached_path = path;
            }
        }
#else
        static List<Node> alloc_path() {
            return new List<Node>();
        }

        static void release_path(List<Node> path) {
        }
#endif

        public RBTree(object hlp) {
            // hlp is INodeHelper<T> for some T
            this.hlp = hlp;
        }

        public void Clear() {
            root = null;
            ++version;
        }

        // if key is already in the tree, return the node associated with it
        // if not, insert new_node into the tree, and return it
        public Node Intern<T>(T key, Node new_node) {
            if (root == null) {
                if (new_node == null)
                    new_node = ((INodeHelper<T>)hlp).CreateNode(key);
                root = new_node;
                root.IsBlack = true;
                ++version;
                return root;
            }

            List<Node> path = alloc_path();
            int in_tree_cmp = find_key(key, path);
            Node retval = path[path.Count - 1];
            if (retval == null) {
                if (new_node == null)
                    new_node = ((INodeHelper<T>)hlp).CreateNode(key);
                retval = do_insert(in_tree_cmp, new_node, path);
            }
            // no need for a try .. finally, this is only used to mitigate allocations
            release_path(path);
            return retval;
        }

        // returns the just-removed node (or null if the value wasn't in the tree)
        public Node Remove<T>(T key) {
            if (root == null)
                return null;

            List<Node> path = alloc_path();
            int in_tree_cmp = find_key(key, path);
            Node retval = null;
            if (in_tree_cmp == 0)
                retval = do_remove(path);
            // no need for a try .. finally, this is only used to mitigate allocations
            release_path(path);
            return retval;
        }

        public Node Lookup<T>(T key) {
            INodeHelper<T> hlp = (INodeHelper<T>)this.hlp;
            Node current = root;
            while (current != null) {
                int c = hlp.Compare(key, current);
                if (c == 0)
                    break;
                current = c < 0 ? current.left : current.right;
            }
            return current;
        }

        public void Bound<T>(T key, ref Node lower, ref Node upper) {
            INodeHelper<T> hlp = (INodeHelper<T>)this.hlp;
            Node current = root;
            while (current != null) {
                int c = hlp.Compare(key, current);
                if (c <= 0)
                    upper = current;
                if (c >= 0)
                    lower = current;
                if (c == 0)
                    break;
                current = c < 0 ? current.left : current.right;
            }
        }

        public int Count {
            get { return root == null ? 0 : (int)root.Size; }
        }

        public Node this[int index] {
            get {
                if (index < 0 || index >= Count)
                    throw new IndexOutOfRangeException("index");

                Node current = root;
                while (current != null) {
                    int left_size = current.left == null ? 0 : (int)current.left.Size;
                    if (index == left_size)
                        return current;
                    if (index < left_size) {
                        current = current.left;
                    }
                    else {
                        index -= left_size + 1;
                        current = current.right;
                    }
                }
                throw new SystemException("Internal Error: index calculation");
            }
        }

        public NodeEnumerator GetEnumerator() {
            return new NodeEnumerator(this);
        }

        // Get an enumerator that starts at 'key' or the next higher element in the tree
        public NodeEnumerator GetSuffixEnumerator<T>(T key) {
            var pennants = new Stack<Node>();
            INodeHelper<T> hlp = (INodeHelper<T>)this.hlp;
            Node current = root;
            while (current != null) {
                int c = hlp.Compare(key, current);
                if (c <= 0)
                    pennants.Push(current);
                if (c == 0)
                    break;
                current = c < 0 ? current.left : current.right;
            }
            return new NodeEnumerator(this, pennants);
        }

        IEnumerator<Node> IEnumerable<Node>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

#if TEST
                public void VerifyInvariants ()
                {
                        if (root != null) {
                                if (!root.IsBlack)
                                        throw new SystemException ("Internal Error: root is not black");
                                root.VerifyInvariants ();
                        }
                }

                public void Dump ()
                {
                        if (root != null)
                                root.Dump ("");
                }
#endif

        // Pre-condition: root != null
        int find_key<T>(T key, List<Node> path) {
            INodeHelper<T> hlp = (INodeHelper<T>)this.hlp;
            int c = 0;
            Node sibling = null;
            Node current = root;

            if (path != null)
                path.Add(root);

            while (current != null) {
                c = hlp.Compare(key, current);
                if (c == 0)
                    return c;

                if (c < 0) {
                    sibling = current.right;
                    current = current.left;
                }
                else {
                    sibling = current.left;
                    current = current.right;
                }

                if (path != null) {
                    path.Add(sibling);
                    path.Add(current);
                }
            }

            return c;
        }

        Node do_insert(int in_tree_cmp, Node current, List<Node> path) {
            path[path.Count - 1] = current;
            Node parent = path[path.Count - 3];

            if (in_tree_cmp < 0)
                parent.left = current;
            else
                parent.right = current;
            for (int i = 0; i < path.Count - 2; i += 2)
                ++path[i].Size;

            if (!parent.IsBlack)
                rebalance_insert(path);

            if (!root.IsBlack)
                throw new SystemException("Internal error: root is not black");

            ++version;
            return current;
        }

        Node do_remove(List<Node> path) {
            int curpos = path.Count - 1;

            Node current = path[curpos];
            if (current.left != null) {
                Node pred = right_most(current.left, current.right, path);
                current.SwapValue(pred);
                if (pred.left != null) {
                    Node ppred = pred.left;
                    path.Add(null); path.Add(ppred);
                    pred.SwapValue(ppred);
                }
            }
            else if (current.right != null) {
                Node succ = current.right;
                path.Add(null); path.Add(succ);
                current.SwapValue(succ);
            }

            curpos = path.Count - 1;
            current = path[curpos];

            if (current.Size != 1)
                throw new SystemException("Internal Error: red-black violation somewhere");

            // remove it from our data structures
            path[curpos] = null;
            node_reparent(curpos == 0 ? null : path[curpos - 2], current, 0, null);

            for (int i = 0; i < path.Count - 2; i += 2)
                --path[i].Size;

            if (current.IsBlack) {
                current.IsBlack = false;
                if (curpos != 0)
                    rebalance_delete(path);
            }

            if (root != null && !root.IsBlack)
                throw new SystemException("Internal Error: root is not black");

            ++version;
            return current;
        }

        // Pre-condition: current is red
        void rebalance_insert(List<Node> path) {
            int curpos = path.Count - 1;
            do {
                // parent == curpos-2, uncle == curpos-3, grandpa == curpos-4
                if (path[curpos - 3] == null || path[curpos - 3].IsBlack) {
                    rebalance_insert__rotate_final(curpos, path);
                    return;
                }

                path[curpos - 2].IsBlack = path[curpos - 3].IsBlack = true;

                curpos -= 4; // move to the grandpa

                if (curpos == 0) // => current == root
                    return;
                path[curpos].IsBlack = false;
            } while (!path[curpos - 2].IsBlack);
        }

        // Pre-condition: current is black
        void rebalance_delete(List<Node> path) {
            int curpos = path.Count - 1;
            do {
                Node sibling = path[curpos - 1];
                // current is black => sibling != null
                if (!sibling.IsBlack) {
                    // current is black && sibling is red
                    // => both sibling.left and sibling.right are black, and are not null
                    curpos = ensure_sibling_black(curpos, path);
                    // one of the nephews became the new sibling -- in either case, sibling != null
                    sibling = path[curpos - 1];
                }

                if ((sibling.left != null && !sibling.left.IsBlack) ||
                 (sibling.right != null && !sibling.right.IsBlack)) {
                    rebalance_delete__rotate_final(curpos, path);
                    return;
                }

                sibling.IsBlack = false;

                curpos -= 2; // move to the parent

                if (curpos == 0)
                    return;
            } while (path[curpos].IsBlack);
            path[curpos].IsBlack = true;
        }

        void rebalance_insert__rotate_final(int curpos, List<Node> path) {
            Node current = path[curpos];
            Node parent = path[curpos - 2];
            Node grandpa = path[curpos - 4];

            uint grandpa_size = grandpa.Size;

            Node new_root;

            bool l1 = parent == grandpa.left;
            bool l2 = current == parent.left;
            if (l1 && l2) {
                grandpa.left = parent.right; parent.right = grandpa;
                new_root = parent;
            }
            else if (l1 && !l2) {
                grandpa.left = current.right; current.right = grandpa;
                parent.right = current.left; current.left = parent;
                new_root = current;
            }
            else if (!l1 && l2) {
                grandpa.right = current.left; current.left = grandpa;
                parent.left = current.right; current.right = parent;
                new_root = current;
            }
            else { // (!l1 && !l2)
                grandpa.right = parent.left; parent.left = grandpa;
                new_root = parent;
            }

            grandpa.FixSize(); grandpa.IsBlack = false;
            if (new_root != parent)
                parent.FixSize(); /* parent is red already, so no need to set it */

            new_root.IsBlack = true;
            node_reparent(curpos == 4 ? null : path[curpos - 6], grandpa, grandpa_size, new_root);
        }

        // Pre-condition: sibling is black, and one of sibling.left and sibling.right is red
        void rebalance_delete__rotate_final(int curpos, List<Node> path) {
            //Node current = path [curpos];
            Node sibling = path[curpos - 1];
            Node parent = path[curpos - 2];

            uint parent_size = parent.Size;
            bool parent_was_black = parent.IsBlack;

            Node new_root;
            if (parent.right == sibling) {
                // if far nephew is black
                if (sibling.right == null || sibling.right.IsBlack) {
                    // => near nephew is red, move it up
                    Node nephew = sibling.left;
                    parent.right = nephew.left; nephew.left = parent;
                    sibling.left = nephew.right; nephew.right = sibling;
                    new_root = nephew;
                }
                else {
                    parent.right = sibling.left; sibling.left = parent;
                    sibling.right.IsBlack = true;
                    new_root = sibling;
                }
            }
            else {
                // if far nephew is black
                if (sibling.left == null || sibling.left.IsBlack) {
                    // => near nephew is red, move it up
                    Node nephew = sibling.right;
                    parent.left = nephew.right; nephew.right = parent;
                    sibling.right = nephew.left; nephew.left = sibling;
                    new_root = nephew;
                }
                else {
                    parent.left = sibling.right; sibling.right = parent;
                    sibling.left.IsBlack = true;
                    new_root = sibling;
                }
            }

            parent.FixSize(); parent.IsBlack = true;
            if (new_root != sibling)
                sibling.FixSize(); /* sibling is already black, so no need to set it */

            new_root.IsBlack = parent_was_black;
            node_reparent(curpos == 2 ? null : path[curpos - 4], parent, parent_size, new_root);
        }

        // Pre-condition: sibling is red (=> parent, sibling.left and sibling.right are black)
        int ensure_sibling_black(int curpos, List<Node> path) {
            Node current = path[curpos];
            Node sibling = path[curpos - 1];
            Node parent = path[curpos - 2];

            bool current_on_left;
            uint parent_size = parent.Size;

            if (parent.right == sibling) {
                parent.right = sibling.left; sibling.left = parent;
                current_on_left = true;
            }
            else {
                parent.left = sibling.right; sibling.right = parent;
                current_on_left = false;
            }

            parent.FixSize(); parent.IsBlack = false;

            sibling.IsBlack = true;
            node_reparent(curpos == 2 ? null : path[curpos - 4], parent, parent_size, sibling);

            // accomodate the rotation
            if (curpos + 1 == path.Count) {
                path.Add(null);
                path.Add(null);
            }

            path[curpos - 2] = sibling;
            path[curpos - 1] = current_on_left ? sibling.right : sibling.left;
            path[curpos] = parent;
            path[curpos + 1] = current_on_left ? parent.right : parent.left;
            path[curpos + 2] = current;

            return curpos + 2;
        }

        void node_reparent(Node orig_parent, Node orig, uint orig_size, Node updated) {
            if (updated != null && updated.FixSize() != orig_size)
                throw new SystemException("Internal error: rotation");

            if (orig == root)
                root = updated;
            else if (orig == orig_parent.left)
                orig_parent.left = updated;
            else if (orig == orig_parent.right)
                orig_parent.right = updated;
            else
                throw new SystemException("Internal error: path error");
        }

        // Pre-condition: current != null
        static Node right_most(Node current, Node sibling, List<Node> path) {
            for (;;) {
                path.Add(sibling);
                path.Add(current);
                if (current.right == null)
                    return current;
                sibling = current.left;
                current = current.right;
            }
        }

        [Serializable]
        public struct NodeEnumerator : IEnumerator, IEnumerator<Node>
        {
            RBTree tree;
            uint version;

            Stack<Node> pennants, init_pennants;

            internal NodeEnumerator(RBTree tree)
                : this() {
                this.tree = tree;
                version = tree.version;
            }

            internal NodeEnumerator(RBTree tree, Stack<Node> init_pennants)
                : this(tree) {
                this.init_pennants = init_pennants;
            }

            public void Reset() {
                check_version();
                pennants = null;
            }

            public Node Current {
                get { return pennants.Peek(); }
            }

            object IEnumerator.Current {
                get {
                    check_current();
                    return Current;
                }
            }

            public bool MoveNext() {
                check_version();

                Node next;
                if (pennants == null) {
                    if (tree.root == null)
                        return false;
                    if (init_pennants != null) {
                        pennants = init_pennants;
                        init_pennants = null;
                        return pennants.Count != 0;
                    }
                    pennants = new Stack<Node>();
                    next = tree.root;
                }
                else {
                    if (pennants.Count == 0)
                        return false;
                    Node current = pennants.Pop();
                    next = current.right;
                }
                for (; next != null; next = next.left)
                    pennants.Push(next);

                return pennants.Count != 0;
            }

            public void Dispose() {
                tree = null;
                pennants = null;
            }

            void check_version() {
                if (tree == null)
                    throw new ObjectDisposedException("enumerator");
                if (version != tree.version)
                    throw new InvalidOperationException("tree modified");
            }

            internal void check_current() {
                check_version();
                if (pennants == null)
                    throw new InvalidOperationException("state invalid before the first MoveNext()");
            }
        }
    }
}