using System;
using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    public unsafe class UnManagedArray<T> : IDisposable, ICloneable where T : unmanaged
    {
        public T* data { get; private set; }
        public int size { get; private set; }

        public UnManagedArray(int size)
        {
            this.size = size;
            data = (T*)NativeFuncs.malloc(size * sizeof(T));
            GC.AddMemoryPressure(size * sizeof(T));
        }

        public int sizeInBytes => size * sizeof(T);
        public void* bytes => (void*)data;

        private void Dispose(bool disposing)
        {
            if (disposing)
                GC.SuppressFinalize(this);

            if (data != null)
            {
                NativeFuncs.free((IntPtr)data);
                GC.RemoveMemoryPressure(size * sizeof(T));
                data = null;
                size = 0;
            }
        }

        public void Dispose() { Dispose(true); }

        ~UnManagedArray() { Dispose(false); }

        public T* get(int i) { return &data[i]; }

        public void zeroMemory()
        {
            NativeFuncs.memset((IntPtr)data, 0, (UIntPtr)(size * sizeof(T)));
        }

        public void realloc(int newSize, bool zeroNew = false)
        {
            int oldSize = size;

            size = newSize;
            data = (T*)NativeFuncs.realloc((IntPtr)data, size * sizeof(T));

            SUtil.ReleaseAssert(data != null);

            GC.RemoveMemoryPressure(oldSize * sizeof(T));
            GC.AddMemoryPressure(size * sizeof(T));

            if (size > oldSize && zeroNew)
            {
                IntPtr start = (IntPtr)(data + oldSize);
                ulong byteCount = (ulong)(size - oldSize) * (ulong)sizeof(T);
                NativeFuncs.memset(start, 0, (UIntPtr)byteCount);
            }
        }

        public void reallocInBytes(int newByteSize, bool zeroNew = false)
        {
            SUtil.ReleaseAssert(newByteSize % sizeof(T) == 0);
            realloc(newByteSize / sizeof(T), zeroNew);
        }

        public virtual object Clone()
        {
            UnManagedArray<T> copy = new UnManagedArray<T>(size * sizeof(T));
            NativeFuncs.memcpy((IntPtr) copy.data, (IntPtr) data, size * sizeof(T));
            return copy;
        }
    }

    public unsafe class UnManagedArray2D<T> : UnManagedArray<T> where T : unmanaged
    {
        public int width { get; private set; }
        public int height { get; private set; }

        public UnManagedArray2D(int width, int height) : base(width * height)
        {
            this.width = width;
            this.height = height;
        }

        public T* get(int x, int y)
        {
            return &data[(y * width) + x];
        }

        public bool isPointValid(int x, int y) { return x >= 0 && x < width && y >= 0 && y < height; }

        public override object Clone()
        {
            UnManagedArray2D<T> copy = new UnManagedArray2D<T>(width, height);
            NativeFuncs.memcpy((IntPtr) copy.data, (IntPtr) data, size * sizeof(T));
            return copy;
        }
    }

    public class Array2D<T>
    {
        public int width { get; private set; }
        public int height { get; private set; }
        public T[] data;

        public Array2D(int width, int height)
        {
            data = new T[width * height];
            this.width = width;
            this.height = height;
        }

        public T get(int x, int y)
        {
            return data[(y * width) + x];
        }

        public void set(int x, int y, T value)
        {
            data[(y * width) + x] = value;
        }

        public bool isPointValid(int x, int y) { return x >= 0 && x < width && y >= 0 && y < height; }
    }
}
