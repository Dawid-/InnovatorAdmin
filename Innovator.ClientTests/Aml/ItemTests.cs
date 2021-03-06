﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Innovator.Client.Tests
{
  [TestClass()]
  public class ItemTests
  {
    [TestMethod()]
    public void PropertySetWithNullableData()
    {
      var aml = ElementFactory.Local;
      var item = aml.Item(aml.Type("Stuff"), aml.Action("edit"));
      DateTime? someDate = null;
      DateTime? someDate2 = new DateTime(2016, 01, 01);
      item.Property("some_date").Set(someDate);
      item.Property("some_date_2").Set(someDate2);
      Assert.AreEqual("<Item type=\"Stuff\" action=\"edit\"><some_date is_null=\"1\" /><some_date_2>2016-01-01T00:00:00</some_date_2></Item>", item.ToAml());
    }

    [TestMethod()]
    public void UtcDateConversion()
    {
      var aml = ElementFactory.Local;
      var item = aml.Item(aml.Type("stuff"), aml.Property("created_on", "2016-05-24T13:22:42"));
      var localDate = item.CreatedOn().AsDateTime().Value;
      var utcDate = item.CreatedOn().AsDateTimeUtc().Value;
      Assert.AreEqual(DateTime.Parse("2016-05-24T13:22:42"), localDate);
      Assert.AreEqual(DateTime.Parse("2016-05-24T17:22:42"), utcDate);
    }
    [TestMethod()]
    public void PropertyItemExtraction()
    {
      var aml = ElementFactory.Local;
      var result = aml.FromXml("<Item type='thing' id='1234'><item_prop type='another' keyed_name='stuff'>12345ABCDE12345612345ABCDE123456</item_prop></Item>");
      var propItem = result.AssertItem().Property("item_prop").AsItem().ToAml();
      Assert.AreEqual("<Item type=\"another\" id=\"12345ABCDE12345612345ABCDE123456\"><keyed_name>stuff</keyed_name><id keyed_name=\"stuff\" type=\"another\">12345ABCDE12345612345ABCDE123456</id></Item>", propItem);
    }
    [TestMethod()]
    public void AmlFromArray()
    {
      var aml = ElementFactory.Local;
      IEnumerable<object> parts = new object[] { aml.Type("stuff"), aml.Attribute("keyed_name", "thingy"), "12345ABCDE12345612345ABCDE123456" };
      var item = aml.Item(aml.Type("Random Thing"),
        aml.Property("item_ref", parts)
      );
      Assert.AreEqual("<Item type=\"Random Thing\"><item_ref type=\"stuff\" keyed_name=\"thingy\">12345ABCDE12345612345ABCDE123456</item_ref></Item>", item.ToAml());
    }

    [TestMethod()]
    public void CloneTest()
    {
      var itemAml = @"<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"">
  <SOAP-ENV:Body>
    <Result>
      <Item type=""ItemType"" typeId=""450906E86E304F55A34B3C0D65C097EA"" id=""F0834BBA6FB64394B78DF5BB725532DD"">
        <created_by_id keyed_name=""_Super User"" type=""User"">
          <Item type=""User"" typeId=""45E899CD2859442982EB22BB2DF683E5"" id=""AD30A6D8D3B642F5A2AFED1A4B02BEFA"">
            <id keyed_name=""_Super User"" type=""User"">AD30A6D8D3B642F5A2AFED1A4B02BEFA</id>
            <first_name>_Super</first_name>
            <itemtype>45E899CD2859442982EB22BB2DF683E5</itemtype>
          </Item>
        </created_by_id>
        <id keyed_name=""Report"" type=""ItemType"">F0834BBA6FB64394B78DF5BB725532DD</id>
        <label xml:lang=""en"">Report</label>
      </Item>
    </Result>
  </SOAP-ENV:Body>
</SOAP-ENV:Envelope>";
      var item = ElementFactory.Local.FromXml(itemAml).AssertItem();
      var clone = item.Clone();
      var cloneAml = clone.ToAml();
      var expected = @"<Item type=""ItemType"" typeId=""450906E86E304F55A34B3C0D65C097EA"" id=""F0834BBA6FB64394B78DF5BB725532DD""><created_by_id keyed_name=""_Super User"" type=""User""><Item type=""User"" typeId=""45E899CD2859442982EB22BB2DF683E5"" id=""AD30A6D8D3B642F5A2AFED1A4B02BEFA""><id keyed_name=""_Super User"" type=""User"">AD30A6D8D3B642F5A2AFED1A4B02BEFA</id><first_name>_Super</first_name><itemtype>45E899CD2859442982EB22BB2DF683E5</itemtype></Item></created_by_id><id keyed_name=""Report"" type=""ItemType"">F0834BBA6FB64394B78DF5BB725532DD</id><label xml:lang=""en"">Report</label></Item>";
      Assert.AreEqual(expected, cloneAml);
    }

    [TestMethod()]
    public void ModelTypeTest()
    {
      var itemAml = @"<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"">
  <SOAP-ENV:Body>
    <Result>
      <Item type=""ItemType"" typeId=""450906E86E304F55A34B3C0D65C097EA"" id=""F0834BBA6FB64394B78DF5BB725532DD"">
        <created_by_id keyed_name=""_Super User"" type=""User"">
          <Item type=""User"" typeId=""45E899CD2859442982EB22BB2DF683E5"" id=""AD30A6D8D3B642F5A2AFED1A4B02BEFA"">
            <id keyed_name=""_Super User"" type=""User"">AD30A6D8D3B642F5A2AFED1A4B02BEFA</id>
            <first_name>_Super</first_name>
            <itemtype>45E899CD2859442982EB22BB2DF683E5</itemtype>
          </Item>
        </created_by_id>
        <id keyed_name=""Report"" type=""ItemType"">F0834BBA6FB64394B78DF5BB725532DD</id>
        <label xml:lang=""en"">Report</label>
      </Item>
    </Result>
  </SOAP-ENV:Body>
</SOAP-ENV:Envelope>";
      var item = ElementFactory.Local.FromXml(itemAml, "some query", null).AssertItem();
      Assert.AreEqual("ItemType", item.GetType().Name);
      Assert.AreEqual("User", item.CreatedById().AsItem().GetType().Name);

      var itemAml2 = @"<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"">
  <SOAP-ENV:Body>
    <Result>
      <Item type=""ItemType"" typeId=""450906E86E304F55A34B3C0D65C097EA"" id=""F0834BBA6FB64394B78DF5BB725532DD"">
        <created_by_id keyed_name=""_Super User"" type=""User"">AD30A6D8D3B642F5A2AFED1A4B02BEFA</created_by_id>
        <id keyed_name=""Report"" type=""ItemType"">F0834BBA6FB64394B78DF5BB725532DD</id>
        <label xml:lang=""en"">Report</label>
      </Item>
    </Result>
  </SOAP-ENV:Body>
</SOAP-ENV:Envelope>";
      item = ElementFactory.Local.FromXml(itemAml2, "some query", null).AssertItem();
      Assert.AreEqual("User", item.CreatedById().AsItem().GetType().Name);
    }

    [TestMethod()]
    public void TestItemCreation_Constructor()
    {
      var aml = ElementFactory.Local;
      var item = aml.Item(aml.Action("get"), aml.Type("Part"),
        aml.Property("is_active_rev", "1"),
        aml.Property("item_number", aml.Attribute("condition", "like"), "905-1954-*")
      );
      Assert.AreEqual("<Item action=\"get\" type=\"Part\"><is_active_rev>1</is_active_rev><item_number condition=\"like\">905-1954-*</item_number></Item>", item.ToString());

      Assert.AreEqual("get", item.Action().Value);
      Assert.AreEqual(true, item.ServerEvents().AsBoolean(true));
      Assert.AreEqual(false, item.ServerEvents().Exists);
      Assert.AreEqual(false, item.IsCurrent().AsBoolean().HasValue);
      Assert.AreEqual("905-1954-*", item.Property("item_number").Value);
      Assert.AreEqual(true, item.Property("is_active_rev").AsBoolean().Value);


      item = aml.Item(aml.Attribute("action", "get"),
        aml.Attribute("type", "Part"),
        aml.Property("is_active_rev", "1"),
        aml.Property("item_number", aml.Attribute("condition", "like"), "905-1954-*")
      );
      Assert.AreEqual("<Item action=\"get\" type=\"Part\"><is_active_rev>1</is_active_rev><item_number condition=\"like\">905-1954-*</item_number></Item>", item.ToString());

      //item = (Item)new Item().Action("get").Type("Part")
      //  .SetProperty("is_active_rev", "1")
      //  .Add(new Property("item_number", new Attribute("condition", "like"), "905-1954-*"));
      //Assert.AreEqual("<Item action=\"get\" type=\"Part\"><is_active_rev>1</is_active_rev><item_number condition=\"like\">905-1954-*</item_number></Item>", (string)item);
    }


    [TestMethod()]
    public void TestItemCreation_Relationships()
    {
      var aml = ElementFactory.Local;
      var item = aml.Item(aml.Type("Part"), aml.Action("get"),
        aml.Property("item_number", aml.Condition(Condition.Like), "905-1954-*"),
        aml.Relationships(
          aml.Item(aml.Type("Part BOM"), aml.Action("get"))
        )
      );

      Assert.AreEqual("<Item type=\"Part\" action=\"get\"><item_number condition=\"like\">905-1954-*</item_number><Relationships><Item type=\"Part BOM\" action=\"get\" /></Relationships></Item>", item.ToString());
    }

    [TestMethod]
    public void LanguageHandling()
    {
      var aml = new ElementFactory(new ServerContext() { LanguageCode = "fr" });
      var item = aml.FromXml("<Item type='Supplier' action='get' select='name' language='en,fr'><thing>All</thing><name xml:lang='fr'>Dell France</name><i18n:name xml:lang='en' xmlns:i18n='http://www.aras.com/I18N'>Dell Computers</i18n:name></Item>").AssertItem();
      Assert.AreEqual("All", item.Property("thing").Value);
      Assert.AreEqual("All", item.Property("thing", "en").Value);
      Assert.AreEqual("All", item.Property("thing", "fr").Value);
      Assert.AreEqual("Dell France", item.Property("name").Value);
      Assert.AreEqual("Dell Computers", item.Property("name", "en").Value);
      Assert.AreEqual("Dell France", item.Property("name", "fr").Value);

      item.Property("test1").Set("1");
      item.Property("test2", "fr").Set("2");
      item.Property("test3", "fr").Set("3");
      item.Property("test3", "en").Set("3.en");
      Assert.AreEqual("<Item type=\"Supplier\" action=\"get\" select=\"name\" language=\"en,fr\"><thing>All</thing><name xml:lang=\"fr\">Dell France</name><i18n:name xml:lang=\"en\" xmlns:i18n=\"http://www.aras.com/I18N\">Dell Computers</i18n:name><test1>1</test1><test2 xml:lang=\"fr\">2</test2><test3 xml:lang=\"fr\">3</test3><i18n:test3 xml:lang=\"en\" xmlns:i18n=\"http://www.aras.com/I18N\">3.en</i18n:test3></Item>",
        item.ToAml());
    }

    [TestMethod()]
    public void LanguageHandling_v2()
    {
      var aml = @"<AML xmlns:i18n='http://www.aras.com/I18N'><Item type='Property' id='1234'>
  <label xml:lang='en'>Finished Part Diameter</label>
  <i18n:label xml:lang='de'>Fertigteildurchmesser</i18n:label>
  <i18n:label xml:lang='en'>Finished Part Diameter</i18n:label>
</Item></AML>";
      var item = ElementFactory.Local.FromXml(aml).AssertItem();
      Assert.AreEqual("Finished Part Diameter", item.Property("label").Value);
      Assert.AreEqual("Finished Part Diameter", item.Property("label", "en").Value);
      Assert.AreEqual("Fertigteildurchmesser", item.Property("label", "de").Value);
    }

    [TestMethod]
    public void VerifyItemCount()
    {
      var aml = @"<SOAP-ENV:Envelope xmlns:SOAP-ENV='http://schemas.xmlsoap.org/soap/envelope/'><SOAP-ENV:Body><Result><Item type='Document' typeId='B88C14B99EF449828C5D926E39EE8B89' id='9370ECBC57DD416A9465F69F1281DB74'><classification>Miscellaneous</classification><config_id keyed_name='heart-1239269 (DOC-171531)' type='Document'>9370ECBC57DD416A9465F69F1281DB74</config_id><copyright>0</copyright><created_by_id keyed_name='Eric Domke' type='User'>2D246C5838644C1C8FD34F8D2796E327</created_by_id><created_on>2016-03-04T14:34:56</created_on><current_state keyed_name='Released' type='Life Cycle State' name='Released'>C363ABDADF8D485393BB89877DBDCFD0</current_state><file_extensions>.jpg</file_extensions><generation>1</generation><has_change_pending>0</has_change_pending><has_files>1</has_files><id keyed_name='heart-1239269 (DOC-171531)' type='Document'>9370ECBC57DD416A9465F69F1281DB74</id><is_active_rev>1</is_active_rev><is_current>1</is_current><is_released>1</is_released><is_template>0</is_template><keyed_name>heart-1239269 (DOC-171531)</keyed_name><lab_controlled_document>0</lab_controlled_document><locked_by_id keyed_name='Eric Domke' type='User'>2D246C5838644C1C8FD34F8D2796E327</locked_by_id><major_rev>001</major_rev><modified_by_id keyed_name='Eric Domke' type='User'>2D246C5838644C1C8FD34F8D2796E327</modified_by_id><modified_on>2016-03-04T14:34:59</modified_on><new_version>1</new_version><not_lockable>0</not_lockable><permission_id keyed_name='New Document' type='Permission'>F0E3A6D242FC4889A9A119EEBC8EC79E</permission_id><release_date>2016-03-04T14:34:56</release_date><spec_regulation>0</spec_regulation><state>Released</state><team_id keyed_name='Owner: Public' type='Team'>2DEF50D558B44ECD9A603759D0B2D0DF</team_id><item_number>DOC-171531</item_number><name>heart-1239269</name><itemtype>B88C14B99EF449828C5D926E39EE8B89</itemtype><viewfile keyed_name='View' type='File'>F7584539F93F4F7F83A6EBF54072E6E4</viewfile></Item></Result><Message><Item id='F7584539F93F4F7F83A6EBF54072E6E4' type='File'><filename>f7584539f93f4f7f83a6ebf54072e6e4.jpg</filename></Item><event name='ids_modified' value='9370ECBC57DD416A9465F69F1281DB74|F7584539F93F4F7F83A6EBF54072E6E4|98F667F9CAB04528843D6D20738C46E6|527C835794B842A8B16E054E35B54F61' /></Message></SOAP-ENV:Body></SOAP-ENV:Envelope>";
      var result = ElementFactory.Local.FromXml(aml);
      Assert.AreEqual(1, result.Items().Count());
    }
  }
}
