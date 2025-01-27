// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json.Tests;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public static partial class ObjectTests
    {
        [Fact]
        public static void VerifyTypeFail()
        {
            Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(1, typeof(string)));
        }

        [Theory]
        [MemberData(nameof(WriteSuccessCases))]
        public static void Write(ITestClass testObj)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            string json;

            {
                testObj.Initialize();
                testObj.Verify();
                json = JsonSerializer.Serialize(testObj, testObj.GetType(), options);
            }

            {
                ITestClass obj = (ITestClass)JsonSerializer.Deserialize(json, testObj.GetType(), options);
                obj.Verify();
            }
        }

        public static IEnumerable<object[]> WriteSuccessCases
        {
            get
            {
                return TestData.WriteSuccessCases;
            }
        }

        [Fact]
        public static void WriteObjectAsObject()
        {
            var obj = new ObjectObject { Object = new object() };
            string json = JsonSerializer.Serialize(obj);
            Assert.Equal(@"{""Object"":{}}", json);
        }

        public class ObjectObject
        {
            public object Object { get; set; }
        }

        [Fact]
        public static void WriteObject_PublicIndexer()
        {
            var indexer = new Indexer();
            indexer[42] = 42;
            indexer.NonIndexerProp = "Value";
            Assert.Equal(@"{""NonIndexerProp"":""Value""}", JsonSerializer.Serialize(indexer));
        }

        private class Indexer
        {
            private int _index = -1;

            public int this[int index]
            {
                get => _index;
                set => _index = value;
            }

            public string NonIndexerProp { get; set; }
        }

        [Fact]
        public static void WriteObjectWorks_ReferenceTypeMissingPublicParameterlessConstructor()
        {
            PublicParameterizedConstructorTestClass parameterless = PublicParameterizedConstructorTestClass.Instance;
            Assert.Equal("{\"Name\":\"42\"}", JsonSerializer.Serialize(parameterless));

            ClassWithInternalParameterlessCtor internalObj = ClassWithInternalParameterlessCtor.Instance;
            Assert.Equal("{\"Name\":\"InstancePropertyInternal\"}", JsonSerializer.Serialize(internalObj));

            ClassWithPrivateParameterlessCtor privateObj = ClassWithPrivateParameterlessCtor.Instance;
            Assert.Equal("{\"Name\":\"InstancePropertyPrivate\"}", JsonSerializer.Serialize(privateObj));

            var list = new CollectionWithoutPublicParameterlessCtor(new List<object> { 1, "foo", false });
            Assert.Equal("[1,\"foo\",false]", JsonSerializer.Serialize(list));

            var envelopeList = new List<object>()
            {
                ConcreteDerivedClassWithNoPublicDefaultCtor.Error("oops"),
                ConcreteDerivedClassWithNoPublicDefaultCtor.Ok<string>(),
                ConcreteDerivedClassWithNoPublicDefaultCtor.Ok<int>(),
                ConcreteDerivedClassWithNoPublicDefaultCtor.Ok()
            };
            Assert.Equal("[{\"ErrorString\":\"oops\",\"Result\":null},{\"Result\":null},{\"Result\":0},{\"ErrorString\":\"ok\",\"Result\":null}]", JsonSerializer.Serialize(envelopeList));
        }

        [Fact]
        public static void WritePolymorphicSimple()
        {
            string json = JsonSerializer.Serialize(new { Prop = (object)new[] { 0 } });
            Assert.Equal(@"{""Prop"":[0]}", json);
        }

        [Fact]
        public static void WritePolymorphicDifferentAttributes()
        {
            string json = JsonSerializer.Serialize(new Polymorphic());
            Assert.Equal(@"{""P1"":"""",""p_3"":""""}", json);
        }

        private class Polymorphic
        {
            public object P1 => "";

            [JsonIgnore]
            public object P2 => "";

            [JsonPropertyName("p_3")]
            public object P3 => "";
        }

        // https://github.com/dotnet/runtime/issues/30814
        [Fact]
        public static void EscapingShouldntStackOverflow()
        {
            var test = new { Name = "\u6D4B\u8A6611" };

            var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string result = JsonSerializer.Serialize(test, options);

            Assert.Equal("{\"name\":\"\u6D4B\u8A6611\"}", result);
        }

        // Regression test for https://github.com/dotnet/runtime/issues/61995
        [Fact]
        public static void WriteObjectWithNumberHandling()
        {
            var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString };
            JsonSerializer.Serialize(new object(), options);
        }

        /// <summary>
        /// This test is constrained to run on Windows and MacOSX because it causes
        /// problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        /// succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        /// time the memory is accessed which triggers the full memory allocation.
        /// Also see <see cref="Utf8JsonWriterTests.WriteLargeJsonToStreamWithoutFlushing"/>
        /// </summary>
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(nameof(Utf8JsonWriterTests.IsX64))]
        [OuterLoop]
        public static void SerializeLargeListOfObjects()
        {
            Dto dto = new()
            {
                Prop1 = int.MaxValue,
                Prop2 = int.MinValue,
                Prop3 = "AC",
                Prop4 = 500,
                Prop5 = int.MaxValue / 2,
                Prop6 = 250M,
                Prop7 = 250M,
                Prop8 = 250M,
                Prop9 = 250M,
                Prop10 = 250M,
                Prop11 = 150M,
                Prop12 = 150M,
                Prop13 = DateTimeOffset.MaxValue,
                Prop14 = DateTimeOffset.MaxValue,
                Prop15 = DateTimeOffset.MaxValue,
                Prop16 = DateTimeOffset.MaxValue,
                Prop17 = 3,
                Prop18 = DateTime.MaxValue,
                Prop19 = DateTime.MaxValue,
                Prop20 = 25000,
                Prop21 = DateTime.MaxValue
            };

            // It takes a little over 4,338,000 items to reach a payload size above the Array.MaxLength value.
            List<Dto> items = Enumerable.Repeat(dto, 4_338_000).ToList();

            try
            {
                JsonSerializer.SerializeToUtf8Bytes(items);
            }
            catch (OutOfMemoryException) { }

            items.AddRange(Enumerable.Repeat(dto, 1000).ToList());
            Assert.Throws<OutOfMemoryException>(() => JsonSerializer.SerializeToUtf8Bytes(items));
        }

        class Dto
        {
            public int Prop1 { get; set; }
            public int Prop2 { get; set; }
            public string Prop3 { get; set; }
            public int Prop4 { get; set; }
            public long Prop5 { get; set; }
            public decimal Prop6 { get; set; }
            public decimal Prop7 { get; set; }
            public decimal Prop8 { get; set; }
            public decimal Prop9 { get; set; }
            public decimal Prop10 { get; set; }
            public decimal Prop11 { get; set; }
            public decimal Prop12 { get; set; }
            public DateTimeOffset Prop13 { get; set; }
            public DateTimeOffset Prop14 { get; set; }
            public DateTimeOffset Prop15 { get; set; }
            public DateTimeOffset Prop16 { get; set; }
            public int Prop17 { get; set; }
            public DateTime Prop18 { get; set; }
            public DateTime Prop19 { get; set; }
            public int Prop20 { get; set; }
            public DateTime Prop21 { get; set; }
        }
    }
}
