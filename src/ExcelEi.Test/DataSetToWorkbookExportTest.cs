﻿// /**********************************************************************************************
// Author:		Vasily Kabanov
// Created		2015-02-19
// Comment		
// **********************************************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using NUnit.Framework;
using ExcelEi.Read;
using OfficeOpenXml;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using ExcelEi.Write;

namespace ExcelEi.Test
{
    public class PocoBase
    {
        protected static readonly Random Random = new Random();

        private static int _idSequence;

        /// <inheritdoc />
        public PocoBase(bool newIdentity = false)
        {
            if (newIdentity)
            {
                Id = Interlocked.Increment(ref _idSequence);

                DateTime = DateTime.Now;
            }
        }

        public int Id { get; set; }

        public DateTime DateTime { get; set; }

        protected static bool TakeChance(double rate = 0.1)
        {
            Check.DoCheckArgument(rate >= 0 && rate <= 1);

            return Random.NextDouble() < rate;
        }
    }

    public class PocoOne : PocoBase
    {
        /// <summary>
        ///     Constructor for reading from storage.
        /// </summary>
        public PocoOne()
        {
        }

        /// <summary>
        ///     Constructor for creating new instances.
        /// </summary>
        public PocoOne(int valueCount)
            : base(true)
        {

            Values = new double?[valueCount];

            for (var i = 0; i < valueCount; ++i)
            {
                if (TakeChance())
                    Values[i] = null;
                else
                    Values[i] = Random.NextDouble() * 500;
            }
        }

        public double?[] Values { get; set; }
    }

    public class PocoThree : PocoOne
    {
        /// <inheritdoc />
        public PocoThree()
        {
        }

        /// <inheritdoc />
        public PocoThree(int valueCount)
            : base(valueCount)
        {
            IntegerFromPocoThree = Random.Next();
        }

        public int IntegerFromPocoThree { get; set; }
    }

    public class PocoTwo : PocoBase
    {
        /// <inheritdoc />
        public PocoTwo(bool newIdentity = false)
            : base(newIdentity)
        {
            if (newIdentity)
            {
                if (!TakeChance())
                    FooString = $"String value #{Id}";

                if (!TakeChance())
                    FooInt = Random.Next(-32000, 32000);

                FooFloat = (float)((Random.NextDouble() - 0.5) * float.MaxValue);

                if (!TakeChance())
                    FieldInt = Random.Next();
            }
        }

        public int? FooInt { get; set; }

        public float FooFloat { get; set; }

        public string FooString { get; set; }

        public int? FieldInt;
    }

    public class PocoOneBaseReader<T> : TableMappingReader<T>
        where T: PocoOne
    {
        /// <inheritdoc />
        public PocoOneBaseReader()
        {
            Map(o => o.Id);
            Map(o => o.DateTime);
            Map(o => o.Values, "Joined Values", ParseJoinedValues);
        }

        private double?[] ParseJoinedValues(object joinedValue)
        {
            return joinedValue
                ?.ToString()
                .Split(',')
                .Select(Parse)
                .ToArray();
        }

        private static double? Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            return double.Parse(value);
        }
    }

    public class PocoOneReader : PocoOneBaseReader<PocoOne>
    {
    }

    public class PocoThreeReader : PocoOneBaseReader<PocoThree>
    {
        /// <inheritdoc />
        public PocoThreeReader()
        {
            Map(o => o.IntegerFromPocoThree);
        }
    }

    public class PocoBaseExportConfigurator<T> : PocoExportConfigurator<T>
        where T: PocoBase
    {
        /// <inheritdoc />
        public PocoBaseExportConfigurator(string sheetName, string dataTableName)
            : base(sheetName, dataTableName)
        {
            AddColumn(o => o.Id);
            AddColumn(o => o.DateTime);
        }
    }

    public class PocoOneExportBaseConfigurator<T> : PocoBaseExportConfigurator<T>
        where T: PocoOne
    {
        /// <inheritdoc />
        public PocoOneExportBaseConfigurator(string sheetName, string dataTableName)
            : base(sheetName, dataTableName)
        {
            AddColumn(o => o.Values != null ? string.Join(",", o.Values.Select(e => e.ToString())) : null, "Joined Values");
            AddCollectionColumns(o => o.Values, 5, "value#{0}");
        }
    }

    public class PocoOneExportConfigurator : PocoOneExportBaseConfigurator<PocoOne>
    {
        /// <inheritdoc />
        public PocoOneExportConfigurator(string sheetName, string dataTableName)
            : base(sheetName, dataTableName)
        {
        }
    }

    public class PocoThreeExportConfigurator : PocoOneExportBaseConfigurator<PocoThree>
    {
        /// <inheritdoc />
        public PocoThreeExportConfigurator(string sheetName)
            : base(sheetName, sheetName)
        {
            AddColumn(o => o.IntegerFromPocoThree);
        }
    }

    [TestFixture]
    public class DataSetToWorkbookExportTest
    {
        private int _sequenceNo;

        private int NextNo() => Interlocked.Increment(ref _sequenceNo);

        private string GetNewOutFilePath(string suffix = "") =>
            Path.Combine(TestContext.CurrentContext.WorkDirectory, $"excelei-{DateTime.Now:MM-dd-HHmmss}-{NextNo()}{suffix}.xlsx");

        private bool _deleteExportedFiles = true;

        [Test]
        public void OneTable()
        {
            var outPath = GetNewOutFilePath("-from-ado.net");
            var workbook = new ExcelPackage(new FileInfo(outPath));

            var dataSet = new DataSet();
            var tableName = "LastSegments";
            var dataTable = dataSet.Tables.Add(tableName);
            dataTable.Columns.Add("Id", typeof(long));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("Authority", typeof(string));
            dataTable.Columns.Add("MaterialLotID", typeof(string));
            dataTable.Columns.Add("LotType", typeof(string));
            dataTable.Columns.Add("Designation", typeof(string));
            dataTable.Columns.Add("SegmentState", typeof(string));

            var designations = new [] {"Production", "R&D", "Baseline"};
            var segmentStates = new[] {"Commenced", "Completed", "Aborted"};
            var rowCount = 1000;

            var random = new Random();

            for (var n = 0; n < rowCount; ++n)
            {
                dataTable.Rows.Add(
                    rowCount * 5 - n
                    , $"Description{n}"
                    , $"Authority{n % 3}"
                    , $"Lot#{n}"
                    , $"LotType{n%2}"
                    , designations[random.Next(0, designations.Length)]
                    , segmentStates[random.Next(0, segmentStates.Length)]);
            }

            var config = new DataSetExportAutoConfig(dataSet);

            var tableConfig = config.GetTableConfig(tableName);
            var columnConfig = tableConfig.GetAutoColumnConfig("Description");
            columnConfig.MinimumWidth = 40;
            columnConfig.WrapText = true;

            tableConfig.SheetName = "Last Segments";

            tableConfig.GetAutoColumnConfig("Authority").AutoFit = true;
            tableConfig.GetAutoColumnConfig("MaterialLotID").AutoFit = true;
            tableConfig.GetAutoColumnConfig("LotType").AutoFit = true;
            tableConfig.GetAutoColumnConfig("Designation").AutoFit = true;

            columnConfig = tableConfig.GetAutoColumnConfig("SegmentState");
            columnConfig.Caption = "Segment State";
            columnConfig.AutoFit = true;
            columnConfig.BackgroundColorExtractor = (d, n) => "Aborted".Equals(((DataRow)d)["SegmentState"]) ? Color.LightCoral : (Color?)null;

            var exporter = new DataSetToWorkbookExporter(config) {DataSet = new DataSetAdapter(dataSet)};

            var start = DateTime.Now;

            exporter.Export(workbook);

            var duration = DateTime.Now - start;

            Console.WriteLine("Duration: {0}", duration);

            workbook.Save();

            TestContext.WriteLine($"Saved {outPath}.");

            workbook.Dispose();
            var readTable = AdoTableReader.GetWorksheetDataTable(outPath);

            Assert.AreEqual(tableConfig.Columns.Count, readTable.Columns.Count);

            for (var i = 0; i < tableConfig.Columns.Count; ++i)
            {
                //ExcelDataReader does not set column name, caption and contains header as first data row
                Assert.AreEqual(tableConfig.Columns[i].Caption, readTable.Rows[0][i]);
                // since header is first data row, only string type will be set correctly
                //Assert.AreEqual(tableConfig.Columns[i].ColumnDataSource.DataType, readTable.Columns[i].DataType);
            }

            Assert.AreEqual(rowCount, readTable.Rows.Count - 1);

            for (int i = 0; i < rowCount; ++i)
            {
                var savedRow = dataTable.Rows[i];
                var readRow = readTable.Rows[i + 1];
                for (var c = 0; c < tableConfig.Columns.Count; ++c)
                {
                    Assert.AreEqual(savedRow[c], readRow[c]);
                }
            }

            if (_deleteExportedFiles)
                File.Delete(outPath);
        }

        [Test]
        public void SimplePoco()
        {
            var outPath = GetNewOutFilePath("-simple");
            var workbook = new ExcelPackage(new FileInfo(outPath));

            var dataSetExportConfig = new DataSetExportAutoConfig();

            const string dataTableName = "One";
            const string sheetName = "OneSheet";

            var configurator = new PocoExportConfigurator<PocoTwo>(sheetName, dataTableName);

            Expression<Func<PocoBase, int>> refId = o => o.Id;
            Expression<Func<PocoBase, DateTime>> refDateTime = o => o.DateTime;
            Expression<Func<PocoTwo, long?>> refInt = o => o.FooInt;
            // implicit conversion from float to double
            Expression<Func<PocoTwo, double?>> refFloat = o => o.FooFloat;
            Expression<Func<PocoTwo, string>> refString = o => o.FooString;
            Expression<Func<PocoTwo, long?>> refFieldInt = o => o.FieldInt;

            var idColumnSource = PocoColumnSourceFactory.Create(refId);
            var dateTimeColumnSource = PocoColumnSourceFactory.Create(refDateTime);

            configurator
                .AddColumn(idColumnSource)
                .AddColumn(dateTimeColumnSource)
                .AddColumn(refInt)
                .AddColumn(refFloat)
                .AddColumn(refString)
                .AddColumn(refFieldInt)
                // same column via reflection; duplicate caption allowed when exporting, but not when importing
                // as the reader would not be able to choose which column to get data from
                .AddColumn<int?>(nameof(PocoTwo.FieldInt), "ReflectionFieldInt")
                // when extracted type is unknown at compile time (type parameter is object), actual type will be resolved via reflection
                .AddColumn<object>(nameof(PocoTwo.FieldInt), "ReflectionFieldIntLateType");

            Assert.AreEqual(typeof(int), configurator.Config.GetAutoColumnConfig("ReflectionFieldIntLateType").ColumnDataSource.DataType);

            dataSetExportConfig.AddSheet(configurator.Config);

            var dataSet = new DataSetAdapter();
            var data1 = Enumerable.Range(0, 100)
                .Select(i => new PocoTwo(true))
                .ToList();

            dataSet.Add(data1, dataTableName);

            var exporter = new DataSetToWorkbookExporter(dataSetExportConfig) {DataSet = dataSet};

            exporter.Export(workbook);

            workbook.Save();
            TestContext.WriteLine($"Saved {outPath}.");

            workbook.Dispose();

            workbook = new ExcelPackage(new FileInfo(outPath));

            var reader = ExcelTableReader.ReadContiguousTableWithHeader(workbook.Workbook.Worksheets[1], 1);

            var pocoReader = new TableMappingReader<PocoTwo>();
            pocoReader
                .Map(o => o.Id)
                .Map(o => o.DateTime)
                .Map(o => o.FooInt)
                .Map(o => o.FooFloat)
                .Map(o => o.FooString)
                .Map(o => o.FieldInt);

            var readPocos = pocoReader.Read(reader);

            CheckEquality(data1, readPocos);

            workbook.Dispose();

            if (_deleteExportedFiles)
                File.Delete(outPath);
        }

        [Test]
        public void ArrayFromPoco()
        {
            var outPath = GetNewOutFilePath("-array");
            var workbook = new ExcelPackage(new FileInfo(outPath));

            var dataSetExportConfig = new DataSetExportAutoConfig();

            Expression<Func<PocoBase, int>> refId = o => o.Id;
            Expression<Func<PocoBase, DateTime>> refDateTime = o => o.DateTime;
            Expression<Func<PocoOne, IList<double?>>> refCollection = o => o.Values;

            // predefined configurator reusing mappings from base class export configurators
            dataSetExportConfig.AddSheet(new PocoOneExportConfigurator("OneSheet", "One").Config);

            // adhoc configurator
            var configurator = new PocoExportConfigurator<PocoOne>("TwoSheet");

            configurator
                .AddInheritedColumn(refDateTime)
                .AddCollectionColumns(refCollection, 10)
                .AddInheritedColumn(refId);

            dataSetExportConfig.AddSheet(configurator.Config);

            var dataSet = new DataSetAdapter();
            var data1 = Enumerable.Range(0, 100)
                .Select(i => new PocoOne(6))
                .ToList();
            var data2 = Enumerable.Range(0, 1000)
                .Select(i => new PocoOne(9))
                .ToList();

            dataSet.Add(data1, "One");
            dataSet.Add(data2, "TwoSheet");

            var exporter = new DataSetToWorkbookExporter(dataSetExportConfig) {DataSet = dataSet};

            exporter.Export(workbook);

            workbook.Save();
            TestContext.WriteLine($"Saved {outPath}.");

            workbook.Dispose();

            workbook = new ExcelPackage(new FileInfo(outPath));

            var reader = ExcelTableReader.ReadContiguousTableWithHeader(workbook.Workbook.Worksheets[1], 1);
            var readPocos = new PocoOneReader().Read(reader);

            CheckEquality(data1, readPocos);

            workbook.Dispose();

            if (_deleteExportedFiles)
                File.Delete(outPath);
        }

        [Test]
        public void InheritedMembers()
        {
            var outPath = GetNewOutFilePath("-inherited");
            var workbook = new ExcelPackage(new FileInfo(outPath));

            var dataSetExportConfig = new DataSetExportAutoConfig();

            var configurator = new PocoExportConfigurator<PocoThree>("OneSheet", "One");

            Expression<Func<PocoBase, int>> refId = o => o.Id;
            Expression<Func<PocoBase, DateTime>> refDateTime = o => o.DateTime;
            Expression<Func<PocoOne, IList<double?>>> refCollection = o => o.Values;
            Expression<Func<PocoOne, string>> refJoinedCollection = o => o.Values != null ? string.Join(",", o.Values.Select(e => e.ToString())) : null;

            configurator
                .AddInheritedColumn(refId)
                .AddInheritedColumn(refDateTime)
                .AddInheritedColumn(refJoinedCollection, "Joined Values")
                .AddColumn(o => o.IntegerFromPocoThree);

            configurator.AddInheritedCollectionColumns(refCollection, 5, "value#{0}");

            dataSetExportConfig.AddSheet(configurator.Config);

            configurator = new PocoExportConfigurator<PocoThree>("TwoSheet");

            configurator.AddInheritedColumn(refDateTime);

            configurator.AddInheritedCollectionColumns(refCollection, 10);

            configurator.AddInheritedColumn(refId);

            dataSetExportConfig.AddSheet(configurator.Config);

            var dataSet = new DataSetAdapter();
            var data1 = Enumerable.Range(0, 100)
                .Select(i => new PocoThree(6))
                .ToList();
            var data2 = Enumerable.Range(0, 1000)
                .Select(i => new PocoThree(9))
                .ToList();

            dataSet.Add(data1, "One");
            dataSet.Add(data2, "TwoSheet");

            var exporter = new DataSetToWorkbookExporter(dataSetExportConfig) {DataSet = dataSet};

            exporter.Export(workbook);

            workbook.Save();
            TestContext.WriteLine($"Saved {outPath}.");

            workbook.Dispose();

            workbook = new ExcelPackage(new FileInfo(outPath));

            var reader = ExcelTableReader.ReadContiguousTableWithHeader(workbook.Workbook.Worksheets[1], 1);
            var readPocos = new PocoThreeReader().Read(reader);

            CheckEquality(data1, readPocos);

            workbook.Dispose();

            if (_deleteExportedFiles)
                File.Delete(outPath);
        }

        [Test]
        public void Polymorphic()
        {
            var outPath = GetNewOutFilePath("-polymorphic");
            var workbook = new ExcelPackage(new FileInfo(outPath));

            var dataSetExportConfig = new DataSetExportAutoConfig();

            var configurator = new PocoExportConfigurator<PocoOne>("OneSheet", "One");

            Expression<Func<PocoBase, int>> refId = o => o.Id;
            Expression<Func<PocoBase, DateTime>> refDateTime = o => o.DateTime;
            Expression<Func<PocoOne, IList<double?>>> refCollection = o => o.Values;
            Expression<Func<PocoOne, string>> refJoinedCollection = o => o.Values != null ? string.Join(",", o.Values.Select(e => e.ToString())) : null;

            Expression<Func<PocoThree, int>> refPocoThreeInt = o => o.IntegerFromPocoThree;

            configurator
                .AddInheritedColumn(refId)
                .AddInheritedColumn(refDateTime)
                .AddColumn(refJoinedCollection, "Joined Values")
                .AddCollectionColumns(refCollection, 5, "value#{0}")
                .AddColumnPolymorphic(refPocoThreeInt);

            dataSetExportConfig.AddSheet(configurator.Config);

            dataSetExportConfig.AddSheet(new PocoThreeExportConfigurator("TwoSheet").Config);

            var dataSet = new DataSetAdapter();
            var data1 = Enumerable.Range(0, 100)
                .Select(i => new PocoThree(6))
                .ToList();
            var data2 = Enumerable.Range(0, 1000)
                .Select(i => new PocoThree(9))
                .ToList();

            dataSet.Add(data1, "One");
            dataSet.Add(data2, "TwoSheet");

            var exporter = new DataSetToWorkbookExporter(dataSetExportConfig) {DataSet = dataSet};

            exporter.Export(workbook);

            workbook.Save();
            TestContext.WriteLine($"Saved {outPath}.");

            workbook.Dispose();

            workbook = new ExcelPackage(new FileInfo(outPath));

            var reader = ExcelTableReader.ReadContiguousTableWithHeader(workbook.Workbook.Worksheets[1], 1);
            var readPocos = new PocoThreeReader().Read(reader);

            CheckEquality(data1, readPocos);

            workbook.Dispose();

            if (_deleteExportedFiles)
                File.Delete(outPath);
        }

        [Test]
        public void SparseColumns()
        {
            var outPath = GetNewOutFilePath("-sparse");
            var workbook = new ExcelPackage(new FileInfo(outPath));

            const string sheetName = "One";
            var exportConfig = new PocoThreeExportConfigurator(sheetName).Config;

            const int firstColumnIndex = 2;
            // this is index of the header row
            const int firstRowIndex = 3;

            exportConfig.LeftSheetColumnIndex = firstColumnIndex;
            exportConfig.TopSheetRowIndex = firstRowIndex;
            // no freezing panes
            exportConfig.FreezeColumnIndex = null;

            // move third column to the right
            Assert.IsNotEmpty(exportConfig.Columns[2].Caption, "Sheet column#2 has no caption");
            var movedColumnConfig = exportConfig.GetAutoColumnConfig(exportConfig.Columns[2].Caption);
            Assert.IsNotNull(movedColumnConfig, "Failed to find column export config by caption");
            movedColumnConfig.Index = exportConfig.Columns.Count + 2;
            // allow it to grow more at the end of the table
            movedColumnConfig.MaximumWidth = 300;

            var dataSetExportConfig = new DataSetExportAutoConfig();
            dataSetExportConfig.AddSheet(exportConfig);

            var pocoList = Enumerable.Range(0, 100)
                .Select(i => new PocoThree(6))
                .ToList();

            var dataSet = new DataSetAdapter().Add(pocoList, sheetName);

            var exporter = new DataSetToWorkbookExporter(dataSetExportConfig) {DataSet = dataSet};
            exporter.Export(workbook);

            workbook.Save();
            TestContext.WriteLine($"Saved {outPath}.");

            workbook.Dispose();

            workbook = new ExcelPackage(new FileInfo(outPath));

            var columnReadingMap = exportConfig.Columns
                .Select(c => new KeyValuePair<string, int>(c.Caption, firstColumnIndex + c.Index))
                .ToList();

            const int startDataRowIndex = firstRowIndex + 1;
            var reader = new ExcelTableReader(workbook.Workbook.Worksheets[1], startDataRowIndex, null, columnReadingMap);
            var readPocos = new PocoThreeReader().Read(reader);

            CheckEquality(pocoList, readPocos);

            workbook.Dispose();

            if (_deleteExportedFiles)
                File.Delete(outPath);
        }

        [Test]
        public void ExplicitColumnIndex()
        {
            var configurator = new PocoExportConfigurator<PocoOne>("dd");

            var sheetColumnIndex = 23;

            var columnConfig = configurator.AddColumnCompiled(o => o.DateTime, sheetColumnIndex, "df", false, "format");

            Assert.IsNotNull(columnConfig);
            Assert.AreEqual(sheetColumnIndex, columnConfig.Index);
            Assert.AreEqual("df", columnConfig.Caption);
            Assert.AreEqual(false, columnConfig.AutoFit);
            Assert.AreEqual("format", columnConfig.Format);

            Assert.Throws<ArgumentException>(() => configurator.AddColumnCompiled(o => o.Id, sheetColumnIndex, "df", false, "format"));

            configurator.AddColumn(o => o.Id, 25, "Id", false, "format");
            columnConfig = configurator.Config.GetAutoColumnConfig("Id");
            Assert.IsNotNull(columnConfig);
            Assert.AreEqual(25, columnConfig.Index);
            Assert.AreEqual("Id", columnConfig.Caption);
            Assert.AreEqual(false, columnConfig.AutoFit);
            Assert.AreEqual("format", columnConfig.Format);
        }

        private void CheckEquality(IList<PocoOne> savedList, IList<PocoOne> readList)
        {
            Assert.AreEqual(savedList.Count, readList.Count, "Count mismatch");

            for (var i = 0; i < savedList.Count; ++i)
                CheckEquality(savedList[i], readList[i]);
        }

        private void CheckEquality(IList<PocoThree> savedList, IList<PocoThree> readList)
        {
            Assert.AreEqual(savedList.Count, readList.Count, "Count mismatch");

            for (var i = 0; i < savedList.Count; ++i)
                CheckEquality(savedList[i], readList[i]);
        }

        private void CheckEquality(IList<PocoTwo> savedList, IList<PocoTwo> readList)
        {
            Assert.AreEqual(savedList.Count, readList.Count, "Count mismatch");

            for (var i = 0; i < savedList.Count; ++i)
                CheckEquality(savedList[i], readList[i]);
        }

        private void CheckEquality(PocoThree saved, PocoThree read)
        {
            CheckEquality((PocoOne)saved, read);

            Assert.AreEqual(saved.IntegerFromPocoThree, read.IntegerFromPocoThree);
        }

        private void CheckEquality(PocoOne saved, PocoOne read)
        {
            CheckEquality((PocoBase)saved, read);

            Assert.AreEqual(saved.Id, read.Id);
            Assert.Less((saved.DateTime - read.DateTime).TotalMilliseconds, 1);

            Assert.AreEqual(saved.Values.Length, read.Values.Length);

            for (var j = 0; j < saved.Values.Length; ++j)
            {
                if (saved.Values[j].HasValue)
                    Assert.AreEqual(saved.Values[j].Value, read.Values[j], 0.0000001D);
                else
                    Assert.IsFalse(read.Values[j].HasValue);
            }
        }

        private void CheckEquality(PocoTwo saved, PocoTwo read)
        {
            CheckEquality((PocoBase)saved, read);

            Assert.AreEqual(saved.FooInt, read.FooInt);
            Assert.AreEqual(saved.FooFloat, read.FooFloat, 0.00000001D);
            Assert.AreEqual(saved.FooString, read.FooString);
        }

        private void CheckEquality(PocoBase saved, PocoBase read)
        {
            Assert.AreEqual(saved.Id, read.Id);
            Assert.Less((saved.DateTime - read.DateTime).TotalMilliseconds, 1);
        }
    }
}
