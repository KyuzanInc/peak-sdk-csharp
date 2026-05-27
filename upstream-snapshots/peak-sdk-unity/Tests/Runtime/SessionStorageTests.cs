using NUnit.Framework;
using Peak.Utils;
using UnityEngine;

namespace Peak.Tests
{
    public class SessionStorageTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [Test]
        public void SaveAndLoad_ReturnsPersistedData()
        {
            var data = new SessionStorage.SessionData
            {
                email = "user@example.com",
                sessionJwt = "jwt-token",
                targetPrivateKey = "private-key",
                targetPublicKey = "public-key",
                savedAt = 123456789
            };

            SessionStorage.Save(data);

            var loaded = SessionStorage.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(data.email, loaded.email);
            Assert.AreEqual(data.sessionJwt, loaded.sessionJwt);
            Assert.AreEqual(data.targetPrivateKey, loaded.targetPrivateKey);
            Assert.AreEqual(data.targetPublicKey, loaded.targetPublicKey);
            Assert.AreEqual(data.savedAt, loaded.savedAt);
        }

        [Test]
        public void Clear_RemovesStoredSession()
        {
            SessionStorage.Save(new SessionStorage.SessionData
            {
                email = "user@example.com",
                sessionJwt = "jwt-token",
                targetPrivateKey = "private-key",
                targetPublicKey = "public-key",
                savedAt = 1
            });

            Assert.IsTrue(SessionStorage.HasSession());

            SessionStorage.Clear();

            Assert.IsFalse(SessionStorage.HasSession());
            Assert.IsNull(SessionStorage.Load());
        }
    }
}
