using System;
using NUnit.Framework;
using UnityEngine;

namespace NovelForge.Runtime.Tests
{
    public class AudioLibraryTests
    {
        private static AudioClip CreateClip(string name) => AudioClip.Create(name, 1000, 1, 44100, false);

        [Test]
        public void TryGetMusicClip_ReturnsTrueAndClip_WhenIdRegistered()
        {
            var clip = CreateClip("theme");
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = clip } };

            bool found = library.TryGetMusicClip("theme_calm", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(clip, result);
        }

        [Test]
        public void TryGetMusicClip_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = CreateClip("theme") } };

            bool found = library.TryGetMusicClip("theme_sad", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetMusicClip_ReturnsFalse_WhenClipReferenceIsUnset()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = null } };

            bool found = library.TryGetMusicClip("theme_calm", out var result);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryGetSfxClip_ReturnsTrueAndClip_WhenIdRegistered()
        {
            var clip = CreateClip("door");
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.sfxClips = new[] { new AudioLibrary.Entry { id = "door_open", clip = clip } };

            bool found = library.TryGetSfxClip("door_open", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(clip, result);
        }

        [Test]
        public void TryGetSfxClip_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();

            bool found = library.TryGetSfxClip("door_open", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }
    }
}
