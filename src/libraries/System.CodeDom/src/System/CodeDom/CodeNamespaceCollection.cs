// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace System.CodeDom
{
    public class CodeNamespaceCollection : CollectionBase
    {
        public CodeNamespaceCollection() { }

        public CodeNamespaceCollection(CodeNamespaceCollection value)
        {
            AddRange(value);
        }

        public CodeNamespaceCollection(CodeNamespace[] value)
        {
            AddRange(value);
        }

        public CodeNamespace this[int index]
        {
            get => (CodeNamespace)List[index];
            set => List[index] = value;
        }

        public int Add(CodeNamespace value) => List.Add(value);

        public void AddRange(CodeNamespace[] value)
        {
            ArgumentNullException.ThrowIfNull(value);

            for (int i = 0; i < value.Length; i++)
            {
                Add(value[i]);
            }
        }

        public void AddRange(CodeNamespaceCollection value)
        {
            ArgumentNullException.ThrowIfNull(value);

            int currentCount = value.Count;
            for (int i = 0; i < currentCount; i++)
            {
                Add(value[i]);
            }
        }

        public bool Contains(CodeNamespace value) => List.Contains(value);

        public void CopyTo(CodeNamespace[] array, int index) => List.CopyTo(array, index);

        public int IndexOf(CodeNamespace value) => List.IndexOf(value);

        public void Insert(int index, CodeNamespace value) => List.Insert(index, value);

        public void Remove(CodeNamespace value) => List.Remove(value);
    }
}
