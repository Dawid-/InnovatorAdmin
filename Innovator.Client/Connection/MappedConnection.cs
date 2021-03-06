﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Innovator.Client.Connection
{
  class MappedConnection : IRemoteConnection
  {
    private IRemoteConnection _current;
    private IEnumerable<ServerMapping> _mappings;
    private ICredentials _lastCredentials;
    private Action<IHttpRequest> _settings;
    private bool _allowAuth;

    public ElementFactory AmlContext { get { return _current == null ? ElementFactory.Local : _current.AmlContext; } }
    public string Database { get { return _current == null ? null : _current.Database; } }
    public Uri Url { get { return _current == null ? null : _current.Url; } }
    public string UserId { get { return _current == null ? null : _current.UserId; } }

    public MappedConnection(IEnumerable<ServerMapping> mappings, bool allowAuth)
    {
      _mappings = mappings;
      _allowAuth = allowAuth;
    }

    public UploadCommand CreateUploadCommand()
    {
      return _current.CreateUploadCommand();
    }

    public void DefaultSettings(Action<IHttpRequest> settings)
    {
      if (_current != null)
        _current.DefaultSettings(settings);
      _settings = settings;
    }

    public void Dispose()
    {
      if (_current != null)
        _current.Dispose();
    }

    public IEnumerable<string> GetDatabases()
    {
      return _mappings.SelectMany(s => s.Databases);
    }

    public void Login(ICredentials credentials)
    {
      Login(credentials, false).Wait();
    }

    public IPromise<string> Login(ICredentials credentials, bool async)
    {
      _lastCredentials = credentials;
      var mapping = _mappings.First(m => m.Databases.Contains(credentials.Database));
      var netCred = credentials as INetCredentials;
      IPromise<ICredentials> credPromise;

      var endpoint = credentials is WindowsCredentials
        ? mapping.Endpoints.AuthWin.Concat(mapping.Endpoints.Auth).First()
        : mapping.Endpoints.Auth.First();

      if (netCred != null && _allowAuth && !string.IsNullOrEmpty(endpoint))
      {
        var http = Factory.DefaultService.Invoke();
        var promise = new Promise<ICredentials>();
        credPromise = promise;

        http.Execute("GET", endpoint, new QueryString() { { "db", credentials.Database } }, netCred.Credentials, async, null)
          .Done(r =>
          {
            var res = r.AsXml().DescendantsAndSelf("Result").FirstOrDefault();
            var user = res.Element("user").Value;
            var pwd = res.Element("password").Value;
            if (pwd.IsNullOrWhiteSpace())
              promise.Reject(new Exception("Failed to authenticate with Innovator server '" + mapping.Url + "'. Original error: " + user));
            var needHash = !string.Equals(res.Element("hash").Value, "false", StringComparison.OrdinalIgnoreCase);
            if (needHash)
            {
              promise.Resolve(new ExplicitCredentials(netCred.Database, user, pwd));
            }
            else
            {
              promise.Resolve(new ExplicitHashCredentials(netCred.Database, user, pwd));
            }
          }).Fail(ex =>
          {
            // Only hard fail for problems which aren't time outs and not found issues.
            var webEx = ex as HttpException;
            var timeout = ex as HttpTimeoutException;
            if (webEx != null && webEx.Response.StatusCode == HttpStatusCode.NotFound)
            {
              promise.Resolve(credentials);
            }
            else if (webEx != null && webEx.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
              promise.Reject(ElementFactory.Local.ServerException("Invalid username or password"));
            }
            else if (timeout != null)
            {
              promise.Resolve(credentials);
            }
            else
            {
              promise.Reject(ex);
            }
          });
      }
      else
      {
        credPromise = Promises.Resolved(credentials);
      }
      _current = mapping.Connection;
      if (_settings != null)
        _current.DefaultSettings(_settings);
      return credPromise.Continue(cred => _current.Login(cred, async));
    }

    public void Logout(bool unlockOnLogout)
    {
      if (_current != null)
        _current.Logout(unlockOnLogout);
      _current = null;
    }

    public void Logout(bool unlockOnLogout, bool async)
    {
      if (_current != null)
        _current.Logout(unlockOnLogout, async);
      _current = null;
    }

    public string MapClientUrl(string relativeUrl)
    {
      return _current.MapClientUrl(relativeUrl);
    }

    public Stream Process(Command request)
    {
      return _current.Process(request);
    }

    public IPromise<Stream> Process(Command request, bool async)
    {
      return _current.Process(request, async);
    }

    public IPromise<IRemoteConnection> Clone(bool async)
    {
      var newConn = new MappedConnection(_mappings, _allowAuth);
      return newConn.Login(_lastCredentials, async)
        .Convert(u => (IRemoteConnection)newConn);
    }
  }
}
