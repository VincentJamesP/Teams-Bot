using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace KTI.PAL.Teams.Core
{
    public static class Common
    {
        /// <summary>
        /// Calculate a hash using sha256.
        /// </summary>
        /// <param name="input">A string input.</param>
        /// <returns>The calculated hash.</returns>
        public static string CalculateHash(string input)
        {
            using (HashAlgorithm h = SHA256.Create())
            {
                StringBuilder s = new();
                foreach (byte b in h.ComputeHash(Encoding.UTF8.GetBytes(input)))
                    s.Append(b.ToString("X2"));
                return s.ToString();
            }
        }

        // https://codereview.stackexchange.com/a/122481
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            int count = 0;
            using (var iter = source.GetEnumerator())
            {
                while (iter.MoveNext())
                {
                    var chunk = new T[size];
                    count = 1;
                    chunk[0] = iter.Current;
                    for (int i = 1; i < size && iter.MoveNext(); i++)
                    {
                        chunk[i] = iter.Current;
                        count++;
                    }
                    if (count < size)
                    {
                        Array.Resize(ref chunk, count);
                    }
                    yield return chunk;
                }
            }
        }
    }
}
