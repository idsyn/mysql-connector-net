// Copyright (c) 2015, 2020 Oracle and/or its affiliates.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License, version 2.0, as
// published by the Free Software Foundation.
//
// This program is also distributed with certain software (including
// but not limited to OpenSSL) that is licensed under separate terms,
// as designated in a particular file or component or in included license
// documentation.  The authors of MySQL hereby grant you an
// additional permission to link the program and your derivative works
// with the separately licensed software that they have included with
// MySQL.
//
// Without limiting anything contained in the foregoing, this file,
// which is part of MySQL Connector/NET, is also subject to the
// Universal FOSS Exception, version 1.0, a copy of which can be found at
// http://oss.oracle.com/licenses/universal-foss-exception.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License, version 2.0, for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using MySql.Data;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using MySqlX.XDevAPI.Relational;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MySqlX.Data.Tests
{
  public class SessionTests : BaseTest
  {
    [Test]
    [Property("Category", "Security")]
    public void CanCloseSession()
    {
      Session s = MySQLX.GetSession(ConnectionString);
      Assert.True(s.InternalSession.SessionState == SessionState.Open);
      s.Close();
      Assert.AreEqual(s.InternalSession.SessionState, SessionState.Closed);
    }

    [Test]
    [Property("Category", "Security")]
    public void NoPassword()
    {
      Session session = MySQLX.GetSession(ConnectionStringNoPassword);
      Assert.True(session.InternalSession.SessionState == SessionState.Open);
      session.Close();
      Assert.AreEqual(session.InternalSession.SessionState, SessionState.Closed);
    }

    [Test]
    [Property("Category", "Security")]
    public void SessionClose()
    {
      Session session = MySQLX.GetSession(ConnectionString);
      Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      session.Close();
      Assert.AreEqual(SessionState.Closed, session.InternalSession.SessionState);
    }

    [Test]
    [Property("Category", "Security")]
    public void CountClosedSession()
    {
      Session nodeSession = MySQLX.GetSession(ConnectionString);
      int sessions = ExecuteSQLStatement(nodeSession.SQL("show processlist")).FetchAll().Count;

      for (int i = 0; i < 20; i++)
      {
        Session session = MySQLX.GetSession(ConnectionString);
        Assert.True(session.InternalSession.SessionState == SessionState.Open);
        session.Close();
        Assert.AreEqual(session.InternalSession.SessionState, SessionState.Closed);
      }

      int newSessions = ExecuteSQLStatement(nodeSession.SQL("show processlist")).FetchAll().Count;
      nodeSession.Close();
      Assert.AreEqual(sessions, newSessions - 1);
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectionStringAsAnonymousType()
    {
      var connstring = new
      {
        server = session.Settings.Server,
        port = session.Settings.Port,
        user = session.Settings.UserID,
        password = session.Settings.Password
      };

      using (var testSession = MySQLX.GetSession(connstring))
      {
        Assert.AreEqual(SessionState.Open, testSession.InternalSession.SessionState);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SessionGetSetCurrentSchema()
    {
      using (Session testSession = MySQLX.GetSession(ConnectionString))
      {
        Assert.AreEqual(SessionState.Open, testSession.InternalSession.SessionState);
        Assert.Null(testSession.GetCurrentSchema());
        Assert.Throws<MySqlException>(() => testSession.SetCurrentSchema(""));
        testSession.SetCurrentSchema(schemaName);
        Assert.AreEqual(schemaName, testSession.Schema.Name);
        Assert.AreEqual(schemaName, testSession.GetCurrentSchema().Name);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SessionUsingSchema()
    {
      using (Session mySession = MySQLX.GetSession(ConnectionString + $";database={schemaName};"))
      {
        Assert.AreEqual(SessionState.Open, mySession.InternalSession.SessionState);
        Assert.AreEqual(schemaName, mySession.Schema.Name);
        Assert.AreEqual(schemaName, mySession.GetCurrentSchema().Name);
        Assert.True(SchemaExistsInDatabase(mySession.Schema));
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SessionUsingDefaultSchema()
    {
      using (Session mySession = MySQLX.GetSession(ConnectionString + $";database={schemaName};"))
      {
        Assert.AreEqual(SessionState.Open, mySession.InternalSession.SessionState);
        Assert.AreEqual(schemaName, mySession.DefaultSchema.Name);
        Assert.AreEqual(schemaName, mySession.GetCurrentSchema().Name);
        Assert.True(mySession.Schema.ExistsInDatabase());
        mySession.SetCurrentSchema("mysql");
        Assert.AreNotEqual(mySession.DefaultSchema.Name, mySession.Schema.Name);
      }

      // DefaultSchema is null because no database was provided in the connection string/URI.
      using (Session mySession = MySQLX.GetSession(ConnectionString))
      {
        Assert.AreEqual(SessionState.Open, mySession.InternalSession.SessionState);
        Assert.Null(mySession.DefaultSchema);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SessionUsingDefaultSchemaWithAnonymousObject()
    {
      var globalSession = GetSession();

      using (var internalSession = MySQLX.GetSession(new
      {
        server = globalSession.Settings.Server,
        port = globalSession.Settings.Port,
        user = globalSession.Settings.UserID,
        password = globalSession.Settings.Password,
        sslmode = MySqlSslMode.Required,
        database = "mysql"
      }))
      {
        Assert.AreEqual("mysql", internalSession.DefaultSchema.Name);
      }

      // DefaultSchema is null when no database is provided.
      using (var internalSession = MySQLX.GetSession(new
      {
        server = globalSession.Settings.Server,
        port = globalSession.Settings.Port,
        user = globalSession.Settings.UserID,
        password = globalSession.Settings.Password,
        sslmode = MySqlSslMode.Required,
      }))
      {
        Assert.Null(internalSession.DefaultSchema);
      }

      // Access denied error is raised when database does not exist for servers 8.0.12 and below.
      // This behavior was fixed since MySql Server 8.0.13 version. Now the error 
      // shows the proper message, "Unknown database..."
      if (session.InternalSession.GetServerVersion().isAtLeast(8, 0, 13)) return;
      var exception = Assert.Throws<MySqlException>(() => MySQLX.GetSession(new
      {
        server = globalSession.Settings.Server,
        port = globalSession.Settings.Port,
        user = globalSession.Settings.UserID,
        password = globalSession.Settings.Password,
        sslmode = MySqlSslMode.Required,
        database = "test1"
      }
      ));

      if (session.InternalSession.GetServerVersion().isAtLeast(8, 0, 13))
        StringAssert.StartsWith(string.Format("Unknown database 'test1'"), exception.Message);
      else
        StringAssert.StartsWith(string.Format("Access denied"), exception.Message);
    }

    [Test]
    [Property("Category", "Security")]
    public void SessionUsingDefaultSchemaWithConnectionURI()
    {
      using (var session = MySQLX.GetSession(ConnectionStringUri + "?database=mysql"))
      {
        Assert.AreEqual("mysql", session.DefaultSchema.Name);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void CheckConnectionUri()
    {
      CheckConnectionData("mysqlx://myuser:password@localhost:33060", "myuser", "password", "localhost", 33060);
      CheckConnectionData("mysqlx://my%3Auser:p%40ssword@localhost:33060", "my:user", "p@ssword", "localhost", 33060);
      CheckConnectionData("mysqlx://my%20user:p%40ss%20word@localhost:33060", "my user", "p@ss word", "localhost", 33060);
      CheckConnectionData("mysqlx:// myuser : p%40ssword@localhost:33060", "myuser", "p@ssword", "localhost", 33060);
      CheckConnectionData("mysqlx://myuser@localhost:33060", "myuser", "", "localhost", 33060);
      CheckConnectionData("mysqlx://myuser:p%40ssword@localhost", "myuser", "p@ssword", "localhost", 33060);
      CheckConnectionData("mysqlx://myuser:p%40ssw%40rd@localhost", "myuser", "p@ssw@rd", "localhost", 33060);
      CheckConnectionData("mysqlx://my%40user:p%40ssword@localhost", "my@user", "p@ssword", "localhost", 33060);
      CheckConnectionData("mysqlx://myuser@localhost", "myuser", "", "localhost", 33060);
      CheckConnectionData("mysqlx://myuser@127.0.0.1", "myuser", "", "127.0.0.1", 33060);
      CheckConnectionData("mysqlx://myuser@[::1]", "myuser", "", "[::1]", 33060);
      CheckConnectionData("mysqlx://myuser:password@[2606:b400:440:1040:bd41:e449:45ee:2e1a]", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060);
      CheckConnectionData("mysqlx://myuser:password@[2606:b400:440:1040:bd41:e449:45ee:2e1a]:33060", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060);
      Assert.Throws<UriFormatException>(() => CheckConnectionData("mysqlx://myuser:password@[2606:b400:440:1040:bd41:e449:45ee:2e1a:33060]", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060));
      Assert.Throws<UriFormatException>(() => CheckConnectionData("mysqlx://myuser:password@2606:b400:440:1040:bd41:e449:45ee:2e1a:33060", "myuser", "password", "[2606:b400:440:1040:bd41:e449:45ee:2e1a]", 33060));
      CheckConnectionData("mysqlx://myuser:password@[fe80::bd41:e449:45ee:2e1a%17]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 33060);
      CheckConnectionData("mysqlx://myuser:password@[(address=[fe80::bd41:e449:45ee:2e1a%17],priority=100)]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 33060);
      CheckConnectionData("mysqlx://myuser:password@[(address=[fe80::bd41:e449:45ee:2e1a%17]:3305,priority=100)]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 3305);
      Assert.Throws<UriFormatException>(() => CheckConnectionData("mysqlx://myuser:password@[(address=fe80::bd41:e449:45ee:2e1a%17,priority=100)]", "myuser", "password", "[fe80::bd41:e449:45ee:2e1a]", 33060));
      CheckConnectionData("mysqlx://myuser@localhost/test", "myuser", "", "localhost", 33060, "database", "test");
      CheckConnectionData("mysqlx://myuser@localhost/test?ssl%20mode=none&connecttimeout=10", "myuser", "", "localhost", 33060, "database", "test", "ssl mode", "None", "connecttimeout", "10");
      CheckConnectionData("mysqlx+ssh://myuser:password@localhost:33060", "myuser", "password", "localhost", 33060);
      CheckConnectionData("mysqlx://_%21%22%23%24s%26%2F%3D-%25r@localhost", "_!\"#$s&/=-%r", "", "localhost", 33060);
      CheckConnectionData("mysql://myuser@localhost", "", "", "", 33060);
      CheckConnectionData("myuser@localhost", "", "", "", 33060);
      Assert.Throws<UriFormatException>(() => CheckConnectionData("mysqlx://uid=myuser;server=localhost", "", "", "", 33060));
      CheckConnectionData("mysqlx://user:password@server.example.com/", "user", "password", "server.example.com", 33060, "ssl mode", "Required");
      CheckConnectionData("mysqlx://user:password@server.example.com/?ssl-ca=(c:%5Cclient.pfx)", "user", "password", "server.example.com", 33060, "ssl mode", "Required", "ssl-ca", "c:\\client.pfx");
      Assert.Throws<NotSupportedException>(() => CheckConnectionData("mysqlx://user:password@server.example.com/?ssl-crl=(c:%5Ccrl.pfx)", "user", "password", "server.example.com", 33060, "ssl mode", "Required", "ssl-crl", "(c:\\crl.pfx)"));
      // tls-version
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=TlSv1.2", "myuser", "password", "localhost", 33060, "tls-version", "Tls12");
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=TlS1.2", "myuser", "password", "localhost", 33060, "tls-version", "Tls12");
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=TlSv12", "myuser", "password", "localhost", 33060, "tls-version", "Tls12");
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=TlS12", "myuser", "password", "localhost", 33060, "tls-version", "Tls12");
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=[ TlSv1.2 ,tLsV11, TLSv1.0 , tls13 ]", "myuser", "password", "localhost", 33060, "tls-version", "Tls, Tls11, Tls12, Tls13");
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=( TlSv1.2 ,tLsV11, TLSv1 , tls13 )", "myuser", "password", "localhost", 33060, "tls-version", "Tls, Tls11, Tls12, Tls13");
      CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version= TlSv1.2 ,tLsV11, TLSv10 , tls13", "myuser", "password", "localhost", 33060, "tls-version", "Tls, Tls11, Tls12, Tls13");
      Assert.Throws<ArgumentException>(() => CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=TlSv1.2,tLsV2.1", "myuser", "password", "localhost", 33060, "tls-version", ""));
      Assert.Throws<ArgumentException>(() => CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=SSL3", "myuser", "password", "localhost", 33060, "tls-version", ""));
      Assert.Throws<ArgumentException>(() => CheckConnectionData("mysqlx://myuser:password@localhost:33060?ssl-mode=none&tls-version=TlsV1.2", "myuser", "password", "localhost", 33060, "tls-version", ""));
      Assert.Throws<ArgumentException>(() => CheckConnectionData("mysqlx://myuser:password@localhost:33060?tls-version=TlsV1.2&ssl-mode=none", "myuser", "password", "localhost", 33060, "tls-version", ""));
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectionUsingUri()
    {
      using (var session = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectionStringNull()
    {
      Assert.Throws<ArgumentNullException>(() => MySQLX.GetSession(null));
    }

    [Test]
    [Property("Category", "Security")]
    public void IPv6()
    {
      var csBuilder = new MySqlXConnectionStringBuilder(ConnectionString);
      csBuilder.Server = "::1";
      csBuilder.Port = uint.Parse(XPort);

      using (var session = MySQLX.GetSession(csBuilder.ToString()))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void IPv6AsUrl()
    {
      var csBuilder = new MySqlXConnectionStringBuilder(ConnectionString);
      string connString = $"mysqlx://{csBuilder.UserID}:{csBuilder.Password}@[::1]:{XPort}";
      using (Session session = MySQLX.GetSession(connString))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void IPv6AsAnonymous()
    {
      var csBuilder = new MySqlXConnectionStringBuilder(ConnectionString);
      using (Session session = MySQLX.GetSession(new { server = "::1", user = csBuilder.UserID, password = csBuilder.Password, port = XPort }))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void CreateSessionWithUnsupportedOptions()
    {
      var errorMessage = "Option not supported.";
      var connectionUri = string.Format("{0}?", ConnectionStringUri);

      // Use a connection URI.
      var ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "pipe=MYSQL"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "compress=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allow batch=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "logging=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "sharedmemoryname=MYSQL"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "defaultcommandtimeout=30"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "usedefaultcommandtimeoutforef=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "persistsecurityinfo=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "encrypt=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "integratedsecurity=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allowpublickeyretrieval=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "autoenlist=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "includesecurityasserts=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allowzerodatetime=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "convert zero datetime=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "useusageadvisor=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "procedurecachesize=50"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "useperformancemonitor=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "ignoreprepare=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "respectbinaryflags=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "treat tiny as boolean=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "allowuservariables=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "interactive=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "functionsreturnstring=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "useaffectedrows=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "oldguids=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "sqlservermode=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "tablecaching=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "defaulttablecacheage=60"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "checkparameters=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "replication=replication_group"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "exceptioninterceptors=none"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "commandinterceptors=none"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "connectionlifetime=100"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "pooling=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "minpoolsize=0"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "maxpoolsize=20"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "connectionreset=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionUri + "cacheserverproperties=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);

      // Use a connection string.
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession("treatblobsasutf8=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession("blobasutf8includepattern=pattern"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession("blobasutf8excludepattern=pattern"));
      StringAssert.StartsWith(errorMessage, ex.Message);
    }

    [Test]
    [Property("Category", "Security")]
    public void CreateBuilderWithUnsupportedOptions()
    {
      var errorMessage = "Option not supported.";
      var ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("pipe=MYSQL"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("allow batch=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("respectbinaryflags=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("pooling=false"));
      StringAssert.StartsWith(errorMessage, ex.Message);
      ex = Assert.Throws<ArgumentException>(() => new MySqlXConnectionStringBuilder("cacheserverproperties=true"));
      StringAssert.StartsWith(errorMessage, ex.Message);
    }

    [Test]
    [Property("Category", "Security")]
    public void GetUri()
    {
      using (var internalSession = MySQLX.GetSession(session.Uri))
      {
        // Validate that all properties keep their original value.
        foreach (var connectionOption in session.Settings.values)
        {
          // SslCrl connection option is skipped since it isn't currently supported.
          if (connectionOption.Key == "sslcrl")
            continue;

          try
          {
            Assert.AreEqual(session.Settings[connectionOption.Key], internalSession.Settings[connectionOption.Key]);
          }
          catch (ArgumentException ex)
          {
            StringAssert.StartsWith("Option not supported.", ex.Message);
          }
        }
      }
    }

    /// <summary>
    /// WL #12177 Implement connect timeout
    /// </summary>
    [Test]
    [Property("Category", "Security")]
    public void ConnectTimeout()
    {
      // Create a session passing the new parameter "connect-timeout" and set it to a valid value.
      // ConnectionString.
      using (Session session = MySQLX.GetSession(ConnectionString + ";connect-timeout=5000;"))
      {
        Assert.True(session.Settings.ConnectTimeout == 5000);
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }

      // ConnectionURI.
      using (Session session = MySQLX.GetSession(ConnectionStringUri + "?connecttimeout=6500"))
      {
        Assert.True(session.Settings.ConnectTimeout == 6500);
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }

      // Anonymous Object using default value, 10000ms.
      var connstring = new
      {
        server = session.Settings.Server,
        port = session.Settings.Port,
        user = session.Settings.UserID,
        password = session.Settings.Password,
        connecttimeout = session.Settings.ConnectTimeout
      };

      using (var testSession = MySQLX.GetSession(connstring))
      {
        Assert.True(session.Settings.ConnectTimeout == 10000);
        Assert.AreEqual(SessionState.Open, testSession.InternalSession.SessionState);
      }

      // Create a session using the fail over functionality passing two diferrent Server address(one of them is fake host). Must succeed after 2000ms
      var conn = $"server=143.24.20.36,127.0.0.1;user=test;password=test;port={XPort};connecttimeout=2000;";
      TestConnectTimeoutSuccessTimeout(conn, 0, 3, "Fail over success");

      // Offline (fake)host using default value, 10000ms.
      conn = "server=143.24.20.36;user=test;password=test;port=33060;";
      TestConnectTimeoutFailureTimeout(conn, 9, 20, "Offline host default value");

      // Offline (fake)host using 15000ms.
      conn = "server=143.24.20.36;user=test;password=test;port=33060;connecttimeout=15000";
      TestConnectTimeoutFailureTimeout(conn, 14, 17, "Offline host 15000ms");

      // Offline (fake)host timeout disabled.
      conn = "server=143.24.20.36;user=test;password=test;port=33060;connecttimeout=0";
      TestConnectTimeoutFailureTimeout(conn, 10, 600, "Offline host timeout disabled");

      // Both (fake)servers offline. Connection must time out after 20000ms
      conn = "server=143.24.20.36,143.24.20.35;user=test;password=test;port=33060;";
      DateTime start = DateTime.Now;
      Assert.Throws<MySqlException>(() => MySQLX.GetSession(conn));
      TimeSpan diff = DateTime.Now.Subtract(start);
      Assert.True(diff.TotalSeconds > 19 && diff.TotalSeconds < 21, String.Format("Timeout exceeded ({0}). Actual time: {1}", "Fail over failure", diff));

      // Valid session no time out
      start = DateTime.Now;
      using (Session session = MySQLX.GetSession(ConnectionStringUri + "?connecttimeout=2000"))
        session.SQL("SELECT SLEEP(10)").Execute();
      diff = DateTime.Now.Subtract(start);
      Assert.True(diff.TotalSeconds > 10);

      //Invalid Values for Connection Timeout parameter
      var ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout=-1;"));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout=foo;"));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout='';"));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout=10.5;"));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout=" + Int32.MaxValue + 1));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout=10.5;"));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionString + ";connect-timeout=;"));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionStringUri + "?connect-timeout= "));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      ex = Assert.Throws<FormatException>(() => MySQLX.GetSession(ConnectionStringUri + "?connecttimeout="));
      Assert.AreEqual(ResourcesX.InvalidConnectionTimeoutValue, ex.Message);

      // Valid value for ConnectionTimeout, invalid credentials
      var exception = Assert.Throws<MySqlException>(() => MySQLX.GetSession("server=localhost;user=test;password=noPass;port=33060;connect-timeout=2000;"));
      Assert.NotNull(exception);
    }

    private void TestConnectTimeoutFailureTimeout(String connString, int minTime, int maxTime, string test)
    {
      DateTime start = DateTime.Now;
      Assert.Throws<TimeoutException>(() => MySQLX.GetSession(connString));
      TimeSpan diff = DateTime.Now.Subtract(start);
      Assert.True(diff.TotalSeconds > minTime && diff.TotalSeconds < maxTime, String.Format("Timeout exceeded ({0}). Actual time: {1}", test, diff));
    }

    private void TestConnectTimeoutSuccessTimeout(String connString, int minTime, int maxTime, string test)
    {
      DateTime start = DateTime.Now;
      MySQLX.GetSession(connString);
      TimeSpan diff = DateTime.Now.Subtract(start);
      Assert.True(diff.TotalSeconds > minTime && diff.TotalSeconds < maxTime, String.Format("Timeout exceeded ({0}). Actual time: {1}", test, diff));
    }

    [Test]
    [Property("Category", "Security")]
    public void MaxConnections()
    {
      try
      {
        List<Session> sessions = new List<Session>();
        ExecuteSqlAsRoot("SET @@global.mysqlx_max_connections = 2");
        for (int i = 0; i <= 2; i++)
        {
          Session newSession = MySQLX.GetSession(ConnectionString);
          sessions.Add(newSession);
        }
        Assert.False(true, "MySqlException should be thrown");
      }
      catch (MySqlException ex)
      {
        Assert.AreEqual(ResourcesX.UnableToOpenSession, ex.Message);
      }
      finally
      {
        ExecuteSqlAsRoot("SET @@global.mysqlx_max_connections = 100");
      }
    }

    protected void CheckConnectionData(string connectionData, string user, string password, string server, uint port, params string[] parameters)
    {
      string result = this.session.ParseConnectionData(connectionData);
      var csbuilder = new MySqlXConnectionStringBuilder(result);
      Assert.True(user == csbuilder.UserID, string.Format("Expected:{0} Current:{1} in {2}", user, csbuilder.UserID, connectionData));
      Assert.True(password == csbuilder.Password, string.Format("Expected:{0} Current:{1} in {2}", password, csbuilder.Password, connectionData));
      Assert.True(server == csbuilder.Server, string.Format("Expected:{0} Current:{1} in {2}", server, csbuilder.Server, connectionData));
      Assert.True(port == csbuilder.Port, string.Format("Expected:{0} Current:{1} in {2}", port, csbuilder.Port, connectionData));
      if (parameters != null)
      {
        if (parameters.Length % 2 != 0)
          throw new ArgumentOutOfRangeException();
        for (int i = 0; i < parameters.Length; i += 2)
        {
          Assert.True(csbuilder.ContainsKey(parameters[i]));
          Assert.AreEqual(parameters[i + 1], csbuilder[parameters[i]].ToString());
        }
      }
    }

    /// <summary>
    /// WL12514 - DevAPI: Support session-connect-attributes
    /// </summary>
    [Test]
    [Property("Category", "Security")]
    public void ConnectionAttributes()
    {
      if (!(session.Version.isAtLeast(8, 0, 16))) return;

      // Validate that MySQLX.GetSession() supports a new 'connection-attributes' query parameter
      // with default values and all the client attributes starts with a '_'.
      TestConnectionAttributes(ConnectionString + ";connection-attributes=true;");
      TestConnectionAttributes(ConnectionStringUri + "?connectionattributes");

      // Validate that no attributes, client or user defined, are sent to server when the value is "false".
      TestConnectionAttributes(ConnectionString + ";connection-attributes=false;");
      TestConnectionAttributes(ConnectionStringUri + "?connectionattributes=false");

      // Validate default behavior with different scenarios.
      TestConnectionAttributes(ConnectionString + ";connection-attributes;");
      TestConnectionAttributes(ConnectionStringUri + "?connectionattributes=true");
      TestConnectionAttributes(ConnectionString + ";connection-attributes=;");
      TestConnectionAttributes(ConnectionStringUri + "?connectionattributes=[]");

      // Validate user-defined attributes to be sent to server.
      Dictionary<string, object> userAttrs = new Dictionary<string, object>
      {
        { "foo", "bar" },
        { "quua", "qux" },
        { "key", null }
      };
      TestConnectionAttributes(ConnectionString + ";connection-attributes=[foo=bar,quua=qux,key]", userAttrs);
      TestConnectionAttributes(ConnectionStringUri + "?connectionattributes=[foo=bar,quua=qux,key=]", userAttrs);

      // Errors
      var ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionString + ";connection-attributes=[_key=value]"));
      Assert.AreEqual(ResourcesX.InvalidUserDefinedAttribute, ex.Message);

      ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionString + ";connection-attributes=123"));
      Assert.AreEqual(ResourcesX.InvalidConnectionAttributes, ex.Message);

      ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionString + ";connection-attributes=[key=value,key=value2]"));
      Assert.AreEqual(string.Format(ResourcesX.DuplicateUserDefinedAttribute, "key"), ex.Message);

      ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(new { server = "localhost", port = 33060, user = "root", connectionattributes = "=" }));

      ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionString + ";connectionattributes=[=bar]"));
      Assert.AreEqual(string.Format(ResourcesX.EmptyKeyConnectionAttribute), ex.Message);
    }

    private void TestConnectionAttributes(string connString, Dictionary<string, object> userAttrs = null)
    {
      string sql = "SELECT * FROM performance_schema.session_account_connect_attrs WHERE PROCESSLIST_ID = connection_id()";

      using (Session session = MySQLX.GetSession(connString))
      {
        Assert.AreEqual(SessionState.Open, session.XSession.SessionState);
        var result = session.SQL(sql).Execute().FetchAll();

        if (session.Settings.ConnectionAttributes == "false")
          CollectionAssert.IsEmpty(result);
        else
        {
          CollectionAssert.IsNotEmpty(result);
          MySqlConnectAttrs clientAttrs = new MySqlConnectAttrs();

          if (userAttrs == null)
          {
            Assert.AreEqual(8, result.Count);

            foreach (Row row in result)
              StringAssert.StartsWith("_", row[1].ToString());
          }
          else
          {
            Assert.AreEqual(11, result.Count);

            for (int i = 0; i < userAttrs.Count; i++)
            {
              Assert.True(userAttrs.ContainsKey(result.ElementAt(i)[1].ToString()));
              Assert.True(userAttrs.ContainsValue(result.ElementAt(i)[2]));
            }
          }
        }
      }
    }

    #region Authentication

    [Test]
    [Property("Category", "Security")]
    public void MySqlNativePasswordPlugin()
    {
      // TODO: Remove when support for caching_sha2_password plugin is included for X DevAPI.
      if (session.InternalSession.GetServerVersion().isAtLeast(8, 0, 4)) return;

      using (var session = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        var result = ExecuteSQLStatement(session.SQL("SELECT `User`, `plugin` FROM `mysql`.`user` WHERE `User` = 'test';")).FetchAll();
        Assert.AreEqual("test", session.Settings.UserID);
        Assert.AreEqual(session.Settings.UserID, result[0][0].ToString());
        Assert.AreEqual("mysql_native_password", result[0][1].ToString());
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectUsingSha256PasswordPlugin()
    {
      using (var session = MySQLX.GetSession("server=localhost;port=33060;user=root;password=;"))
      {
        ExecuteSQLStatement(session.SQL("DROP USER IF EXISTS 'testSha256'@'localhost';"));
        ExecuteSQLStatement(session.SQL("CREATE USER 'testSha256'@'localhost' identified with sha256_password by 'mysql';"));
        ExecuteSQLStatement(session.SQL("GRANT ALL PRIVILEGES  ON *.*  TO 'testSha256'@'localhost';"));
      }

      string userName = "testSha256";
      string password = "mysql";
      string pluginName = "sha256_password";
      string connectionStringUri = ConnectionStringUri.Replace("test:test", string.Format("{0}:{1}", userName, password));

      // User with password over TLS connection.
      using (var session = MySQLX.GetSession(connectionStringUri))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        var result = ExecuteSQLStatement(session.SQL(string.Format("SELECT `User`, `plugin` FROM `mysql`.`user` WHERE `User` = '{0}';", userName))).FetchAll();
        Assert.AreEqual(userName, session.Settings.UserID);
        Assert.AreEqual(session.Settings.UserID, result[0][0].ToString());
        Assert.AreEqual(pluginName, result[0][1].ToString());
      }

      // Connect over non-TLS connection.
      using (var session = MySQLX.GetSession(connectionStringUri + "?sslmode=none"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }

      // User without password over TLS connection.
      ExecuteSQL(String.Format("ALTER USER {0}@'localhost' IDENTIFIED BY ''", userName));
      using (var session = MySQLX.GetSession(ConnectionStringUri.Replace("test:test", string.Format("{0}:{1}", userName, ""))))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        var result = ExecuteSQLStatement(session.SQL(string.Format("SELECT `User`, `plugin` FROM `mysql`.`user` WHERE `User` = '{0}';", userName))).FetchAll();
        Assert.AreEqual(userName, session.Settings.UserID);
        Assert.AreEqual(session.Settings.UserID, result[0][0].ToString());
        Assert.AreEqual(pluginName, result[0][1].ToString());
      }

      using (var session = MySQLX.GetSession("server=localhost;port=33060;user=root;password=;"))
        ExecuteSQLStatement(session.SQL("DROP USER IF EXISTS 'testSha256'@'localhost';"));
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectUsingExternalAuth()
    {
      // Should fail since EXTERNAL is currently not supported by X Plugin.
      Exception ex = Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionString + ";auth=EXTERNAL"));
      Assert.AreEqual("Invalid authentication method EXTERNAL", ex.Message);
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectUsingPlainAuth()
    {
      using (var session = MySQLX.GetSession(ConnectionStringUri + "?auth=pLaIn"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.PLAIN, session.Settings.Auth);
      }

      // Should fail since PLAIN requires TLS to be enabled.
      Assert.Throws<MySqlException>(() => MySQLX.GetSession(ConnectionStringUri + "?auth=PLAIN&sslmode=none"));
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectUsingMySQL41Auth()
    {
      var connectionStringUri = ConnectionStringUri;
      if (session.InternalSession.GetServerVersion().isAtLeast(8, 0, 4))
      {
        // Use connection string uri set with a mysql_native_password user.
        connectionStringUri = ConnectionStringUriNative;
      }

      using (var session = MySQLX.GetSession(connectionStringUri + "?auth=MySQL41"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.MYSQL41, session.Settings.Auth);
      }

      using (var session = MySQLX.GetSession(connectionStringUri + "?auth=mysql41&sslmode=none"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.MYSQL41, session.Settings.Auth);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void DefaultAuth()
    {
      if (!session.InternalSession.GetServerVersion().isAtLeast(8, 0, 5)) return;

      string user = "testsha256";

      ExecuteSQLStatement(session.SQL($"DROP USER IF EXISTS {user}@'localhost'"));
      ExecuteSQLStatement(session.SQL($"CREATE USER {user}@'localhost' IDENTIFIED WITH caching_sha2_password BY '{user}'"));

      string connString = $"mysqlx://{user}:{user}@localhost:{XPort}";
      // Default to PLAIN when TLS is enabled.
      using (var session = MySQLX.GetSession(connString))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.PLAIN, session.Settings.Auth);
        var result = ExecuteSQLStatement(session.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';")).FetchAll();
        StringAssert.StartsWith("TLSv1", result[0][1].ToString());
      }

      // Default to SHA256_MEMORY when TLS is not enabled.
      using (var session = MySQLX.GetSession(connString + "?sslmode=none"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void ConnectUsingSha256Memory()
    {
      if (!session.InternalSession.GetServerVersion().isAtLeast(8, 0, 5)) return;

      using (var session = MySQLX.GetSession(ConnectionStringUri + "?auth=SHA256_MEMORY"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }

      using (var session = MySQLX.GetSession(ConnectionStringUri + "?auth=SHA256_MEMORY&sslmode=none"))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
        Assert.AreEqual(MySqlAuthenticationMode.SHA256_MEMORY, session.Settings.Auth);
      }
    }

    #endregion

    #region SSL

    [Test]
    [Property("Category", "Security")]
    public void SSLSession()
    {
      using (var s3 = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.AreEqual(SessionState.Open, s3.InternalSession.SessionState);
        var result = ExecuteSQLStatement(s3.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';")).FetchAll();
        StringAssert.StartsWith("TLSv1", result[0][1].ToString());
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLCertificate()
    {
      string path = "../../../../MySql.Data.Tests/";
      string connstring = ConnectionStringUri + $"/?ssl-ca={path}client.pfx&ssl-ca-pwd=pass";
      using (var s3 = MySQLX.GetSession(connstring))
      {
        Assert.AreEqual(SessionState.Open, s3.InternalSession.SessionState);
        var result = ExecuteSQLStatement(s3.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';")).FetchAll();
        StringAssert.StartsWith("TLSv1", result[0][1].ToString());
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLEmptyCertificate()
    {
      string connstring = ConnectionStringUri + $"/?ssl-ca=";
      // if certificate is empty, it connects without a certificate
      using (var s1 = MySQLX.GetSession(connstring))
      {
        Assert.AreEqual(SessionState.Open, s1.InternalSession.SessionState);
        var result = ExecuteSQLStatement(s1.SQL("SHOW SESSION STATUS LIKE 'Mysqlx_ssl_version';")).FetchAll();
        StringAssert.StartsWith("TLSv1", result[0][1].ToString());
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLCrl()
    {
      string connstring = ConnectionStringUri + "/?ssl-crl=crlcert.pfx";
      Assert.Throws<NotSupportedException>(() => MySQLX.GetSession(connstring));
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLOptions()
    {
      string connectionString = ConnectionStringUri;
      // sslmode is valid.
      using (var connection = MySQLX.GetSession(connectionString + "?sslmode=required"))
      {
        Assert.AreEqual(SessionState.Open, connection.InternalSession.SessionState);
      }

      using (var connection = MySQLX.GetSession(connectionString + "?ssl-mode=required"))
      {
        Assert.AreEqual(SessionState.Open, connection.InternalSession.SessionState);
      }

      // sslenable is invalid.
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?sslenable"));
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?ssl-enable"));

      // sslmode=Required is default value.
      using (var connection = MySQLX.GetSession(connectionString))
      {
        Assert.AreEqual(connection.Settings.SslMode, MySqlSslMode.Required);
      }

      // sslmode case insensitive.
      using (var connection = MySQLX.GetSession(connectionString + "?SsL-mOdE=required"))
      {
        Assert.AreEqual(SessionState.Open, connection.InternalSession.SessionState);
      }
      using (var connection = MySQLX.GetSession(connectionString + "?SsL-mOdE=VeRiFyca&ssl-ca=../../../../MySql.Data.Tests/client.pfx&ssl-ca-pwd=pass"))
      {
        Assert.AreEqual(SessionState.Open, connection.InternalSession.SessionState);
        var uri = connection.Uri;
      }

      // Duplicate SSL connection options send error message.
      ArgumentException ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?sslmode=Required&ssl mode=None"));
      StringAssert.EndsWith("is duplicated.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?ssl-ca-pwd=pass&ssl-ca-pwd=pass"));
      StringAssert.EndsWith("is duplicated.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?certificatepassword=pass&certificatepassword=pass"));
      StringAssert.EndsWith("is duplicated.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?certificatepassword=pass&ssl-ca-pwd=pass"));
      StringAssert.EndsWith("is duplicated.", ex.Message);

      // send error if sslmode=None and another ssl parameter exists.
      Assert.Throws<ArgumentException>(() => MySQLX.GetSession(connectionString + "?sslmode=None&ssl-ca=../../../../MySql.Data.Tests/certificates/client.pfx"));
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLRequiredByDefault()
    {
      using (var connection = MySQLX.GetSession(ConnectionStringUri))
      {
        Assert.AreEqual(MySqlSslMode.Required, connection.Settings.SslMode);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLPreferredIsInvalid()
    {
      ArgumentException ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(ConnectionStringUri + "?ssl-mode=Preferred"));
      Assert.AreEqual("Value 'Preferred' is not of the correct type.", ex.Message);
      ex = Assert.Throws<ArgumentException>(() => MySQLX.GetSession(ConnectionStringUri + "?ssl-mode=Prefered"));
      Assert.AreEqual("Value 'Prefered' is not of the correct type.", ex.Message);
    }

    [Test]
    [Property("Category", "Security")]
    public void SSLCertificatePathKeepsCase()
    {
      var certificatePath = "../../../../MySql.Data.Tests/client.pfx";
      // Connection string in basic format.
      string connString = ConnectionString + ";ssl-ca=" + certificatePath + ";ssl-ca-pwd=pass;";
      var stringBuilder = new MySqlXConnectionStringBuilder(connString);
      Assert.AreEqual(certificatePath, stringBuilder.CertificateFile);
      Assert.AreEqual(certificatePath, stringBuilder.SslCa);
      Assert.True(stringBuilder.ConnectionString.Contains(certificatePath));
      connString = stringBuilder.ToString();
      Assert.True(connString.Contains(certificatePath));

      // Connection string in uri format.
      string connStringUri = ConnectionStringUri + "/?ssl-ca=" + certificatePath + "& ssl-ca-pwd=pass;";
      using (var session = MySQLX.GetSession(connStringUri))
      {
        Assert.AreEqual(certificatePath, session.Settings.CertificateFile);
        Assert.AreEqual(certificatePath, session.Settings.SslCa);
        Assert.True(session.Settings.ConnectionString.Contains(certificatePath));
        connString = session.Settings.ToString();
        Assert.True(connString.Contains(certificatePath));
      }
    }

    // Fix Bug 24510329 - UNABLE TO CONNECT USING TLS/SSL OPTIONS FOR THE MYSQLX URI SCHEME
    [TestCase("../../../../MySql.Data.Tests/client.pfx")]
    [TestCase("(../../../../MySql.Data.Tests/client.pfx)")]
    [TestCase(@"(..\..\..\..\MySql.Data.Tests\client.pfx")]
    [TestCase("..\\..\\..\\..\\MySql.Data.Tests\\client.pfx")]
    [Property("Category", "Security")]
    public void SSLCertificatePathVariations(string certificatePath)
    {
      string connStringUri = ConnectionStringUri + "/?ssl-ca=" + certificatePath + "& ssl-ca-pwd=pass;";

      using (var session = MySQLX.GetSession(connStringUri))
      {
        Assert.AreEqual(SessionState.Open, session.InternalSession.SessionState);
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void GetUriWithSSLParameters()
    {
      var session = GetSession();

      var builder = new MySqlXConnectionStringBuilder();
      builder.Server = session.Settings.Server;
      builder.UserID = session.Settings.UserID; ;
      builder.Password = session.Settings.Password;
      builder.Port = session.Settings.Port;
      builder.ConnectionProtocol = MySqlConnectionProtocol.Tcp;
      builder.Database = session.Settings.Database;
      builder.CharacterSet = session.Settings.CharacterSet;
      builder.SslMode = MySqlSslMode.Required;
      builder.SslCa = "../../../../MySql.Data.Tests/client.pfx";
      builder.CertificatePassword = "pass";
      builder.ConnectTimeout = 10000;
      builder.Keepalive = 10;
      builder.Auth = MySqlAuthenticationMode.AUTO;

      var connectionString = builder.ConnectionString;
      string uri = null;

      // Create session with connection string.
      using (var internalSession = MySQLX.GetSession(connectionString))
      {
        uri = internalSession.Uri;
      }

      // Create session with the uri version of the connection string.
      using (var internalSession = MySQLX.GetSession(uri))
      {
        // Compare values of the connection options.
        foreach (string connectionOption in builder.Keys)
        {
          // SslCrl connection option is skipped since it isn't currently supported.
          if (connectionOption == "sslcrl")
            continue;

          // Authentication mode AUTO/DEFAULT is internally assigned, hence it is expected to be different in this scenario. 
          if (connectionOption == "auth")
            Assert.AreEqual(MySqlAuthenticationMode.PLAIN, internalSession.Settings[connectionOption]);
          else
            Assert.AreEqual(builder[connectionOption], internalSession.Settings[connectionOption]);
        }
      }
    }

    [Test]
    [Property("Category", "Security")]
    public void GetUriKeepsSSLMode()
    {
      var globalSession = GetSession();
      var builder = new MySqlXConnectionStringBuilder();
      builder.Server = globalSession.Settings.Server;
      builder.UserID = globalSession.Settings.UserID;
      builder.Password = globalSession.Settings.Password;
      builder.Port = globalSession.Settings.Port;
      builder.Database = "test";
      builder.CharacterSet = globalSession.Settings.CharacterSet;
      builder.SslMode = MySqlSslMode.VerifyCA;
      // Setting SslCa will also set CertificateFile.
      builder.SslCa = TestContext.CurrentContext.TestDirectory + "\\client.pfx";
      builder.CertificatePassword = "pass";
      builder.ConnectTimeout = 10000;
      builder.Keepalive = 10;
      // Auth will change to the authentication mode internally used PLAIN, MySQL41, SHA256_MEMORY: 
      builder.Auth = MySqlAuthenticationMode.AUTO;
      // Doesn't show in the session.URI because Tcp is the default value. Tcp, Socket and Sockets are treated the same.
      builder.ConnectionProtocol = MySqlConnectionProtocol.Tcp;

      string uri = null;
      using (var internalSession = MySQLX.GetSession(builder.ConnectionString))
      {
        uri = internalSession.Uri;
      }

      using (var internalSession = MySQLX.GetSession(uri))
      {
        Assert.AreEqual(builder.Server, internalSession.Settings.Server);
        Assert.AreEqual(builder.UserID, internalSession.Settings.UserID);
        Assert.AreEqual(builder.Password, internalSession.Settings.Password);
        Assert.AreEqual(builder.Port, internalSession.Settings.Port);
        Assert.AreEqual(builder.Database, internalSession.Settings.Database);
        Assert.AreEqual(builder.CharacterSet, internalSession.Settings.CharacterSet);
        Assert.AreEqual(builder.SslMode, internalSession.Settings.SslMode);
        Assert.AreEqual(builder.SslCa, internalSession.Settings.SslCa);
        Assert.AreEqual(builder.CertificatePassword, internalSession.Settings.CertificatePassword);
        Assert.AreEqual(builder.ConnectTimeout, internalSession.Settings.ConnectTimeout);
        Assert.AreEqual(builder.Keepalive, internalSession.Settings.Keepalive);
        Assert.AreEqual(MySqlAuthenticationMode.PLAIN, internalSession.Settings.Auth);
      }
    }

    [TestCase("[]", null)]
    [TestCase("Tlsv1", "TLSv1")]
    [TestCase("Tlsv1.0, Tlsv1.1", "TLSv1.1")]
    [TestCase("Tlsv1.0, Tlsv1.1, Tlsv1.2", "TLSv1.2")]
    //#if NET48 || NETCOREAPP3_1
    // [TestCase("Tlsv1.3", "Tlsv1.3", Skip = "Waiting for full support")]
    //[TestCase("Tlsv1.0, Tlsv1.1, Tlsv1.2, Tlsv1.3", "Tlsv1.3", Skip = "Waiting for full support")]
#if NET452
    [TestCase("Tlsv1.3", "")]
    [TestCase("Tlsv1.0, Tlsv1.1, Tlsv1.2, Tlsv1.3", "Tlsv1.2")]
#endif
    [Property("Category", "Security")]
    public void TlsVersionTest(string tlsVersion, string result)
    {
      var globalSession = GetSession();
      var builder = new MySqlXConnectionStringBuilder();
      builder.Server = globalSession.Settings.Server;
      builder.UserID = globalSession.Settings.UserID;
      builder.Password = globalSession.Settings.Password;
      builder.Port = globalSession.Settings.Port;
      builder.Database = "test";
      void SetTlsVersion() { builder.TlsVersion = tlsVersion; }
      if (result == null)
      {
        Assert.That(SetTlsVersion, Throws.Exception);
        return;
      }

      SetTlsVersion();

      string uri = null;
      if (!String.IsNullOrWhiteSpace(result))
      {
        using (var internalSession = MySQLX.GetSession(builder.ConnectionString))
        {
          uri = internalSession.Uri;
          Assert.AreEqual(SessionState.Open, internalSession.InternalSession.SessionState);
          StringAssert.AreEqualIgnoringCase(result, internalSession.SQL("SHOW SESSION STATUS LIKE 'mysqlx_ssl_version'").Execute().FetchAll()[0][1].ToString());
        }
        using (var internalSession = MySQLX.GetSession(uri))
        {
          Assert.AreEqual(SessionState.Open, internalSession.InternalSession.SessionState);
          StringAssert.AreEqualIgnoringCase(result, internalSession.SQL("SHOW SESSION STATUS LIKE 'mysqlx_ssl_version'").Execute().FetchAll()[0][1].ToString());
        }
      }
      else
#if NET452
        Assert.Throws<NotSupportedException>(() => MySQLX.GetSession(builder.ConnectionString));
#else
        Assert.Throws<System.ComponentModel.Win32Exception>(() => MySQLX.GetSession(builder.ConnectionString));
#endif
    }
    #endregion
  }
}
