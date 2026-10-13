using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyPatcher.Utils
{
    public static class LZSS_c
    {
        const int N = 1024;
        const int F = 66;
        const int THRESHOLD = 2;
        const int NIL = N;

        static byte[] text_buf = new byte[N + F - 1];
        static int match_position, match_length;
        static int[] lson = new int[N + 1];
        static int[] rson = new int[N + 257];
        static int[] dad = new int[N + 1];

        static void InitTree()
        {
            for (int i = N + 1; i <= N + 256; i++)
                rson[i] = NIL;

            for (int i = 0; i < N; i++)
                dad[i] = NIL;
        }

        static void InsertNode(int r)
        {
            int i, p, cmp;
            cmp = 1;
            int keyIndex = r;
            p = N + 1 + text_buf[keyIndex];

            rson[r] = lson[r] = NIL;
            match_length = 0;

            while (true)
            {
                if (cmp >= 0)
                {
                    if (rson[p] != NIL) p = rson[p];
                    else
                    {
                        rson[p] = r;
                        dad[r] = p;
                        return;
                    }
                }
                else
                {
                    if (lson[p] != NIL) p = lson[p];
                    else
                    {
                        lson[p] = r;
                        dad[r] = p;
                        return;
                    }
                }

                for (i = 1; i < F; i++)
                {
                    cmp = text_buf[keyIndex + i] - text_buf[p + i];
                    if (cmp != 0) break;
                }

                if (i > match_length)
                {
                    match_position = p;
                    if ((match_length = i) >= F) break;
                }
            }

            dad[r] = dad[p];
            lson[r] = lson[p];
            rson[r] = rson[p];

            dad[lson[p]] = r;
            dad[rson[p]] = r;

            if (rson[dad[p]] == p)
                rson[dad[p]] = r;
            else
                lson[dad[p]] = r;

            dad[p] = NIL;
        }

        static void DeleteNode(int p)
        {
            int q;

            if (dad[p] == NIL) return;

            if (rson[p] == NIL)
                q = lson[p];
            else if (lson[p] == NIL)
                q = rson[p];
            else
            {
                q = lson[p];
                if (rson[q] != NIL)
                {
                    do { q = rson[q]; } while (rson[q] != NIL);

                    rson[dad[q]] = lson[q];
                    dad[lson[q]] = dad[q];

                    lson[q] = lson[p];
                    dad[lson[p]] = q;
                }

                rson[q] = rson[p];
                dad[rson[p]] = q;
            }

            dad[q] = dad[p];

            if (rson[dad[p]] == p)
                rson[dad[p]] = q;
            else
                lson[dad[p]] = q;

            dad[p] = NIL;
        }

        public static int Encode(byte[] dst, byte[] src, int srcSize)
        {
            int i, c, len, r, s, last_match_length, code_buf_ptr;
            byte[] code_buf = new byte[17];
            byte mask;

            int src_pos = 0;
            int out_pos = 0;

            InitTree();

            code_buf[0] = 0;
            code_buf_ptr = 1;
            mask = 1;

            s = 0;
            r = N - F;

            for (i = s; i < r; i++)
                text_buf[i] = 0;

            for (len = 0; len < F && src_pos < srcSize; len++)
                text_buf[r + len] = src[src_pos++];

            if (len == 0) return 0;

            for (i = 1; i <= F; i++)
                InsertNode(r - i);

            InsertNode(r);

            do
            {
                if (match_length > len) match_length = len;

                if (match_length <= THRESHOLD)
                {
                    match_length = 1;
                    code_buf[0] |= mask;
                    code_buf[code_buf_ptr++] = text_buf[r];
                }
                else
                {
                    code_buf[code_buf_ptr++] = (byte)match_position;
                    code_buf[code_buf_ptr++] =
                        (byte)(((match_position >> 2) & 0xC0) |
                        (match_length - (THRESHOLD + 1)));
                }

                if ((mask <<= 1) == 0)
                {
                    for (i = 0; i < code_buf_ptr; i++)
                        dst[out_pos++] = code_buf[i];

                    code_buf[0] = 0;
                    code_buf_ptr = 1;
                    mask = 1;
                }

                last_match_length = match_length;

                for (i = 0; i < last_match_length && src_pos < srcSize; i++)
                {
                    DeleteNode(s);
                    c = src[src_pos++];
                    text_buf[s] = (byte)c;

                    if (s < F - 1)
                        text_buf[s + N] = (byte)c;

                    s = (s + 1) & (N - 1);
                    r = (r + 1) & (N - 1);

                    InsertNode(r);
                }

                while (i++ < last_match_length)
                {
                    DeleteNode(s);
                    s = (s + 1) & (N - 1);
                    r = (r + 1) & (N - 1);

                    if (--len > 0)
                        InsertNode(r);
                }

            } while (len > 0);

            if (code_buf_ptr > 1)
            {
                for (i = 0; i < code_buf_ptr; i++)
                    dst[out_pos++] = code_buf[i];
            }

            return out_pos;
        }
    }
}
