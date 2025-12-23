// MIT License
// Copyright (c) 2025 NikolyKozemiakin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Buffers;


/// <summary>
/// Lightweight HashSet/List with minimal allocations, using Span and ArrayPool.
/// Suitable for small datasets.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public ref struct DistinctListSlim<T> where T : struct, IEquatable<T>
{
    private Span<T> _span;            // current buffer (external span or rented array)
    private T[] _arrayFromPool;       // rented array from ArrayPool if growth occurred
    private int _count;
    private readonly bool _distinct;

    public bool Distinct => _distinct;

    /// <summary>
    /// Initializes <see cref="DistinctListSlim{T}"/> with an external buffer.
    /// </summary>
    /// <param name="initialBuffer">External buffer. Lifetime must be longer than the list itself.</param>
    /// <param name="distinct">Whether to enforce uniqueness of elements.</param>
    public DistinctListSlim(Span<T> initialBuffer, bool distinct = true)
    {
        if (initialBuffer.IsEmpty) throw new ArgumentException("Initial buffer must not be empty.", nameof(initialBuffer));

        _span = initialBuffer;
        _arrayFromPool = null;
        _count = 0;
        _distinct = distinct;
    }

    /// <summary>Number of elements in the list.</summary>
    public int Count => _count;

    /// <summary>Returns elements as ReadOnlySpan.</summary>
    public ReadOnlySpan<T> AsSpan() => _span.Slice(0, _count);

    /// <summary>Clears the list (Count = 0), without releasing rented array.</summary>
    public void Clear() => _count = 0;

    /// <summary>
    /// Releases rented array (if any) and marks the struct as invalid.
    /// After calling, Add/Remove/Contains/Clear must not be used.
    /// </summary>
    public void Release()
    {
        if (_arrayFromPool != null)
        {
            ArrayPool<T>.Shared.Return(_arrayFromPool, clearArray: true);
            _arrayFromPool = null;
        }
        _span = Span<T>.Empty;
        _count = 0;
    }

    /// <summary>Checks if the list contains an element.</summary>
    public bool Contains(T item)
    {
        EnsureValid();
        return _span.Slice(0, _count).IndexOf(item) >= 0;
    }

    /// <summary>
    /// Adds an element. If Distinct = true, only adds if not present.
    /// </summary>
    public bool Add(T item)
    {
        EnsureValid();

        if (Distinct && Contains(item)) return false;

        if (_count >= _span.Length) Grow();

        _span[_count++] = item;
        return true;
    }

    /// <summary>
    /// Adds multiple elements.
    /// If Distinct = true, only adds unique ones.
    /// If Distinct = false, pre‑expands buffer once.
    /// </summary>
    public void AddRange(ReadOnlySpan<T> span)
    {
        EnsureValid();
        if (span.Length == 0) return;

        if (Distinct)
        {
            for (int j = 0; j < span.Length; j++)
            {
                T item = span[j];
                bool exists = false;
                for (int k = 0; k < _count; k++)
                {
                    if (_span[k].Equals(item))
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists) continue;

                if (_count >= _span.Length) Grow();
                _span[_count++] = item;
            }
        }
        else
        {
            int needed = _count + span.Length;
            if (_span.Length < needed) Grow(needed);

            span.CopyTo(_span.Slice(_count));
            _count += span.Length;
        }
    }

    /// <summary>
    /// Removes an element. Last element is moved into removed slot (order not preserved).
    /// </summary>
    public bool Remove(T item)
    {
        EnsureValid();

        int idx = _span.Slice(0, _count).IndexOf(item);
        if (idx < 0) return false;

        _count--;
        if (idx < _count) _span[idx] = _span[_count];
        return true;
    }

    /// <summary>
    /// Expands capacity to fit more elements.
    /// </summary>
    private void Grow(int minCapacity = 0)
    {
        EnsureValid();

        const int minSize = 4;
        int increment = _span.Length >> 2; // 25% growth
        if (increment == 0) increment = minSize;

        int newSize = _span.Length + increment;
        if (minCapacity > newSize) newSize = minCapacity;

        var newArray = ArrayPool<T>.Shared.Rent(newSize);
        _span.Slice(0, _count).CopyTo(newArray);

        if (_arrayFromPool != null)
            ArrayPool<T>.Shared.Return(_arrayFromPool, clearArray: true);

        _arrayFromPool = newArray;
        _span = _arrayFromPool.AsSpan();
    }

    /// <summary>
    /// Ensures struct is still valid (not released).
    /// Throws ObjectDisposedException if released.
    /// </summary>
    private void EnsureValid()
    {
        if (_span.IsEmpty) throw new ObjectDisposedException(nameof(DistinctListSlim<T>));
    }
}
