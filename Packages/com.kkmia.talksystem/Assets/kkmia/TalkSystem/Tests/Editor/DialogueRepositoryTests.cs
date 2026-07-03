using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueRepositoryTests
    {
        [Test]
        public void GetAll_PreservesCsvRowOrder()
        {
            var csv = "Id,Speaker,Text,NextId\n5,A,First,-1\n3,A,Second,-1\n8,A,Third,-1\n";
            var repo = new DialogueRepository(new TextAsset(csv));

            var ids = repo.GetAll().Select(d => d.Id).ToArray();

            CollectionAssert.AreEqual(new[] { 5, 3, 8 }, ids);
        }

        [Test]
        public void GetByTriggerKey_ReturnsFirstRowOnDuplicateKey()
        {
            // 同一 TriggerKey が複数行に存在する場合、CSV で先に出現する行が選ばれること
            // （Dictionary.Values の列挙順に依存しない: regression issue #40）。
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey\n" +
                      "5,A,First,-1,,greet\n" +
                      "3,A,Second,-1,,greet\n";
            var repo = new DialogueRepository(new TextAsset(csv));

            var data = repo.GetByTriggerKey("greet");

            Assert.IsNotNull(data);
            Assert.AreEqual(5, data.Id);
        }
    }
}
