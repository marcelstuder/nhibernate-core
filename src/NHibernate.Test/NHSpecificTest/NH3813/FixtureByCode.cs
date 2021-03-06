﻿using System.Collections.Generic;
using System.Linq;
using NHibernate.Mapping.ByCode;
using NUnit.Framework;

namespace NHibernate.Test.NHSpecificTest.NH3813
{
	[TestFixture]
	public class KeyManyToOneInnerJoinFetchFixture : TestCaseMappingByCode
	{
		protected override Cfg.MappingSchema.HbmMapping GetMappings()
		{
			var mapper = new ModelMapper();

			mapper.Class<FirstTable>(
				m =>
				{
					m.Lazy(false);

					m.Id(
						i => i.Id,
						id =>
						{
							id.Column("ID");
							id.Generator(Generators.Identity);
						});
					m.Property(x => x.Name);

					m.Bag(
						b => b.AssociationTableCollection,
						bm =>
						{
							bm.Inverse(true);
							bm.Key(k => k.Column("FirstTableID"));
						},
						mp => mp.OneToMany());
				});

			mapper.Class<OtherTable>(
				m =>
				{
					m.Lazy(false);

					m.Id(
						i => i.Id,
						id =>
						{
							id.Column("ID");
							id.Generator(Generators.Identity);
						});
					m.Property(x => x.Name);
				});

			mapper.Class<AssociationTable>(
				m =>
				{
					m.ComposedId(
						i =>
						{
							i.ManyToOne(c => c.FirstTable, p => { p.Column("FirstTableID"); });
							i.ManyToOne(c => c.OtherTable, p => { p.Column("OtherTableID"); });
						});
					m.Property(x => x.Name);
				});

			return mapper.CompileMappingForAllExplicitlyAddedEntities();
		}

		[Test]
		public void FetchQueryDoesNotContainInnerJoin()
		{
			using (var logSpy = new SqlLogSpy())
			using (var session = OpenSession())
			{
				var q = session.QueryOver<FirstTable>()
								.Fetch(SelectMode.Fetch, f => f.AssociationTableCollection)
								.Left.JoinQueryOver(f => f.AssociationTableCollection).List();

				var sql = logSpy.GetWholeLog();
				Assert.That(sql, Is.Not.Contains("inner join"));
			}
		}

		[Test]
		public void FetchQueryDoesNotContainInnerJoinMulti()
		{
			using (var logSpy = new SqlLogSpy())
			using (var session = OpenSession())
			{
				var q = session.QueryOver<FirstTable>()
								.Fetch(SelectMode.Fetch, f => f.AssociationTableCollection)
								.Left.JoinQueryOver<AssociationTable>(f => f.AssociationTableCollection)
								.Left.JoinQueryOver(a => a.OtherTable).List();

				var sql = logSpy.GetWholeLog();

				Assert.That(sql, Does.Not.Contain("inner join"));
				Assert.That(sql, Does.Match(@"join\s*AssociationTable").IgnoreCase);
				Assert.That(sql, Does.Match(@"join\s*OtherTable"));
			}
		}

		[Test]
		public void FetchLoadsAllRecords()
		{
			IList<FirstTable> result = null;

			using (var session = OpenSession())
			{
				// the query should return all records from the table with their collections fetched
				result = session.QueryOver<FirstTable>()
								.Fetch(SelectMode.Fetch, f => f.AssociationTableCollection)
								.Left.JoinQueryOver(f => f.AssociationTableCollection)
								.List();
			}

			Assert.AreEqual(2, result.Count, "Query returned wrong number of records.");
			Assert.IsTrue(result.All(x => NHibernateUtil.IsInitialized(x.AssociationTableCollection)), "Not all collections have been initialized");
		}

		[Test]
		public void FetchInitializesAllCollections()
		{
			IList<FirstTable> result = null;

			using (var session = OpenSession())
			{
				// load all records
				result = session.QueryOver<FirstTable>()
								.List();

				// lazy-load the association collection
				session.QueryOver<FirstTable>()
						.Fetch(SelectMode.Fetch, f => f.AssociationTableCollection)
						.Left.JoinQueryOver(f => f.AssociationTableCollection)
						.List();
			}

			Assert.IsTrue(result.All(x => NHibernateUtil.IsInitialized(x.AssociationTableCollection)), "Not all collections have been initialized");
		}

		protected override void OnSetUp()
		{
			using (var s = OpenSession())
			using (var t = s.BeginTransaction())
			{
				// a record that has association records will be loaded regularly
				var withAssociations = new FirstTable();

				var other1 = new OtherTable();
				var other2 = new OtherTable();

				var assoc1 = new AssociationTable() {OtherTable = other1, FirstTable = withAssociations};
				var assoc2 = new AssociationTable() {OtherTable = other2, FirstTable = withAssociations};

				withAssociations.AssociationTableCollection.Add(assoc1);
				withAssociations.AssociationTableCollection.Add(assoc2);
				s.Save(withAssociations);

				// a record with no associations will have problems if inner joined to association table
				var withoutAssociations = new FirstTable();
				s.Save(withoutAssociations);

				t.Commit();
			}
		}

		protected override void OnTearDown()
		{
			using (var s = OpenSession())
			using (var t = s.BeginTransaction())
			{
				s.CreateQuery("delete from System.Object").ExecuteUpdate();
				t.Commit();
			}
		}
	}
}
