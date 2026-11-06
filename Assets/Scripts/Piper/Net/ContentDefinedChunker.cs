using System;
using System.Collections.Generic;

namespace PipeWorld.Piper
{
    // Content defined chunking
    // https://joshleeb.com/posts/content-defined-chunking.html
    // https://joshleeb.com/posts/gear-hashing.html
    public static unsafe class ContentDefinedChunker
    {
        // pulled from /dev/urandom: length 256
        private static readonly ulong[] gearTable = new ulong[]
        {
            0x81de5a15cdb8b27dUL, 0x1b5ab7cb30ec02e6UL, 0xa9a53428dd2a179fUL, 0x0ee7fa90c4a79302UL, 0xa8fc493f9f88578fUL, 0x3f2b5e7fe3b8f45dUL, 0x3c1e4cc33d11a07fUL, 0xec668c10b4c92f32UL,
            0xf4fed992becdef83UL, 0xddfce07b871e2f48UL, 0x7ba8f6b51296ac7eUL, 0xaa0aded31ff69e50UL, 0x3f04943b45ce017bUL, 0x87a3b505ba89714bUL, 0x2624200263c5c6e6UL, 0x36a9cd0b5057f60dUL,
            0xe8f5467b1c7d9b7eUL, 0x72679239fb29292eUL, 0xa3093e927df93b0cUL, 0xf6d0eda5605e72b3UL, 0x65628a90ee1e8d08UL, 0xeab12f96401d1202UL, 0x0b5aeb1fba3aeaa6UL, 0x5db7491cb8ee2acfUL,
            0xd7308d660d506954UL, 0xc928428abd762328UL, 0xc5d8614330a81382UL, 0xa5e56742e2106ef3UL, 0xbdfa18b018d98c1aUL, 0x73ae7fcbb0821139UL, 0xd838317d61473c0eUL, 0xb802d60b55040f52UL,
            0x7fc3ced34ba501aaUL, 0x86cf50fb7fad9986UL, 0x44ab2848385b0f51UL, 0x221eedb5c845fbe7UL, 0x1ce486de84992103UL, 0x8bd8d43e2c0dc3d4UL, 0xb7d5dcf6bb7aca65UL, 0x504ee4bdb6c9b230UL,
            0x6aed041918d0bc9eUL, 0x2f22c9c94b6d3d52UL, 0x67abb9d41af1fcefUL, 0x94519a1da06cf072UL, 0x1f56f3348fd29fddUL, 0x9a3b39063532b496UL, 0x1773c78babfd2a9aUL, 0xadeab47c1db86350UL,
            0x80659023e53e4174UL, 0x20088e3c286fb3b0UL, 0x932fb1332db6534cUL, 0xac4f4153dad94ca9UL, 0x36397f9dfe5866ccUL, 0x11e7527d404150feUL, 0xb35da4868b52ea88UL, 0x6481752d9a5c7840UL,
            0x1bdcb66e52021a58UL, 0x20dba0aa55c7ba4aUL, 0x0e6b8d86f568a6a1UL, 0x8a1c233dfc96f433UL, 0xefcd8dc33cadfb80UL, 0x85e18960c6ae4f1eUL, 0x9aa6677ce00a8b7cUL, 0x35d67984a321d0d7UL,
            0x82d8da3db83a5a30UL, 0x307464f761257d4fUL, 0xa6e04587df15a0c4UL, 0xb53ff957586c7c17UL, 0x6f2730ef3a8df185UL, 0x11381787c8df8217UL, 0xbaf64fc45691d163UL, 0x415538ee3b58cb7cUL,
            0xa312bf0cfbebb626UL, 0x0a51dccd1164268aUL, 0x882d892b35da7b48UL, 0xf21560cea728cebcUL, 0x1c7840ef6873627bUL, 0xb0868c7a92c4b361UL, 0xf20805ce8dfce945UL, 0x19c4ee546d17be52UL,
            0x2164a2fd86cbaad7UL, 0x33368e21baf0a848UL, 0x8c0234e0588c3afeUL, 0xd459c3edbb7e80ffUL, 0xbe91db740226a36fUL, 0x8cfcc1b1916b71acUL, 0x1fe1e4e4a066d7f9UL, 0x87ff4021454b4924UL,
            0x935f2cddc3ec8a8bUL, 0xd823e7bf2658d66cUL, 0xcd7b083b7b411158UL, 0x5a2359bbdbcc60aeUL, 0x54b05e0e6c8d6edfUL, 0x3c3ae4c8975a8ee0UL, 0x9a1d593d2a28bfd4UL, 0x556a12a9eae78670UL,
            0xcc8c67ae2ae18a91UL, 0x83f4a146b56eea3eUL, 0x0edde04197537b87UL, 0x5379674f27a9e15eUL, 0x301d5ab34881db24UL, 0x798766769d5a710aUL, 0xa6120bf6c0476aa3UL, 0x050adc57d1a897ffUL,
            0x62cc27355d3237dcUL, 0xf02fc4769f5e96e0UL, 0x8b978040723b934aUL, 0x8f793e4b0acf7b6bUL, 0x7aabd6ac0e1852eeUL, 0x3bf54ed4ccac7f51UL, 0x262847adb98c6001UL, 0xed54b8e46fde2e10UL,
            0x1b2edf1469d1de92UL, 0x6c819a54881c9322UL, 0x17c8a16b8847517eUL, 0xc6a58df8d5f2083dUL, 0x9409b3ed2611759fUL, 0x3e2555be1f6189f2UL, 0xbbdb25e6c19e84b3UL, 0xa8622fc76d37c8b0UL,
            0x056b5192a78b1f8eUL, 0x32ddd1281998bbabUL, 0x7912c77cd0b69860UL, 0x9e610ed1556ef797UL, 0xeb6ac7a16ad1ffc0UL, 0xa560ae29a01a2c3aUL, 0x802ecbe7c5873176UL, 0x5dec80a2f4ce3208UL,
            0x4e363c9a7ade5d28UL, 0xd6687c884b75cc9dUL, 0xee64f5a43b951025UL, 0xbc85adb7b7042e86UL, 0xa1a7517683d6f946UL, 0x08cba8d497ccfebaUL, 0xc7e8b47da3e714efUL, 0x875bb15b456f11abUL,
            0x9f06325b85982742UL, 0x4d119233321c824aUL, 0x554f5a927dea1bfcUL, 0xf6959b333be17aa9UL, 0x842707619049d75fUL, 0x72ec0f736d7a87e2UL, 0x279f434bc6960cd8UL, 0x7ed6c78f91ae0009UL,
            0x5a1016993b262aceUL, 0x80aee2c400b96cfcUL, 0x93b1660ae83823ddUL, 0x0964449752c4e3aaUL, 0xb45cc58a2057942cUL, 0x5e55363b51ea4872UL, 0xc95266e2d9f9c1ffUL, 0x9bcea4dcebb97726UL,
            0x1b73a10bdce801e4UL, 0xcc3a91b09d70ca28UL, 0x389d3715d26ed9ffUL, 0xffa9d1b469031aefUL, 0x684ef4d423ca7991UL, 0x461b09273dfb1b32UL, 0x9dd4d48df6cc60bdUL, 0xa2d39fe67b19e279UL,
            0xef3d829e5e9887dcUL, 0xf5b0e8f15e1e07a8UL, 0xb9c72e9789555f92UL, 0xadf274b093860981UL, 0x14fe03981a386b49UL, 0x8ac807629a155d0aUL, 0xd4adcb9160d0bc57UL, 0x5082b1c229f1e3c8UL,
            0xfed87bb1cea92b22UL, 0xb6dc084ef3aa2e0bUL, 0x0ce7ad2114366a8dUL, 0xd2b8aa350ddd7bb7UL, 0x1567c574b00d6a08UL, 0x40da790304b7788eUL, 0x05fd045e368ee058UL, 0x63e2ca0277aff807UL,
            0x34f933a8658897d5UL, 0x77bd026b2a34c62fUL, 0x11985afda433eadfUL, 0x03b817186c528368UL, 0x97500593495a2569UL, 0xdead67c0bb93d3deUL, 0x624e5132c39501b0UL, 0xe824985760743730UL,
            0x3d9de7699d233574UL, 0x307ec46732c2d5dbUL, 0x2eac9e202f36d9d9UL, 0x2e5be3b37660c323UL, 0xd6408078ec9c0274UL, 0x5f63989961d85a3eUL, 0xcbf11c355921f6a0UL, 0xc93ca03b9254fad0UL,
            0x892d365fcc3c77b6UL, 0x1479563791dac713UL, 0x1cf1a3b0c24ed921UL, 0x6951f03f1b460ec2UL, 0x569ac294480be1cdUL, 0xe505ee040b172302UL, 0x73997d066fb17603UL, 0x2a11ae64254cf67bUL,
            0x27e242cdfbeb0d92UL, 0x5e11490b0507ae0dUL, 0x70b5a2b46dc641f1UL, 0x4b9206c808511f02UL, 0xcac80c0532937d76UL, 0xdc39943600b27a5bUL, 0x0ba51403ab375527UL, 0x05da498c8d298d19UL,
            0xc16c1ff8c7d6e517UL, 0xc91f90a5f243c2afUL, 0xe6d858fe49bf346aUL, 0xd965d1a0ddeb9aceUL, 0x5231c7d6b544e41bUL, 0xd4f20283b9b3041aUL, 0x37a0f95ac667f93dUL, 0xc0a6556eb5cdaa8aUL,
            0x80df56c7b8728420UL, 0x264b042462cc60adUL, 0x3c3ce093939711e3UL, 0xbcfac9cd31bfcfd0UL, 0xcd0187a2d9e6c0ceUL, 0xf069c98b92bdc353UL, 0x161b5d33eb908e3eUL, 0xfe20013400d60134UL,
            0x3549864b4192416aUL, 0xaef888cab73a0e1eUL, 0x51e8ca06d6b31860UL, 0x2dc9da9347d083b3UL, 0x80df91ec22da9164UL, 0xd69ce7be23bcfdd8UL, 0x336e7bd157938b61UL, 0x21f805a4e055cd9bUL,
            0xd928639370f30c54UL, 0x591849385336071cUL, 0xb11e527f9b09e887UL, 0xa36c372dd3c20557UL, 0xa490bd50daa02447UL, 0x6efc119ea74ad134UL, 0x618b86efa1cf970eUL, 0x3112c38d4ac0d280UL,
            0x679d11179464136cUL, 0x1acb1f6ed67ce777UL, 0x2517488dc06be469UL, 0xc688bed3fa5a6ab6UL, 0x96bfd8e1918df282UL, 0xf0f5ed36f9b70c47UL, 0xf8c17adb4bfa9bdfUL, 0xf4f421996ee12caaUL,
            0x0db9bfa7a518f301UL, 0xd980ba92e25a0ac4UL, 0x4d2f661034f9cfb7UL, 0x18ee65e9fb5c3393UL, 0x23f9e2628cf517cfUL, 0x8f7eb9f741fe2596UL, 0x0bec27f86069e374UL, 0x0b80c5297893ceefUL,
        };

        public static void split(byte* data, int length, List<ContentDefinedChunkRef> outChunks)
        {
            outChunks.Clear();

            if (length == 0)
                return;

            int start = 0;
            ulong h = 0;

            const ulong boundaryMask = 0b111111111; // 9 1s, 2**9 = 512, average chunk size will be 512 bytes
            const int minimumSize = 10;

            for (int i = 0; i < length; i++)
            {
                h = unchecked((h << 1) + gearTable[data[i]]);

                int chunkSize = i - start + 1;
                if (chunkSize >= minimumSize && (h & boundaryMask) == 0)
                {
                    outChunks.Add(new ContentDefinedChunkRef { offset = start, length = chunkSize });
                    start = i + 1;
                    h = 0;
                }
            }

            if (start < length)
                outChunks.Add(new ContentDefinedChunkRef { offset = start, length = length - start });
        }
    }

    public struct ContentDefinedChunkRef
    {
        public int offset;
        public int length;
    }
}
