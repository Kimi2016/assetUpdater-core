﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using LitJson;
using UnityEngine;

namespace Meow.AssetUpdater.Core
{
    public class UpdateOperation : CustomYieldInstruction
    {
        public float SingleProgress{ get { return _downloadOperation == null ? 0 : _downloadOperation.Progress; } }
        
        public float TotalProgress { get { return (float)TotalownloadedSize / TotalSize; } }
        
        public int SingleDownloadedSize { get { return _downloadOperation == null || _currentUpdatingBundle.Value == null ? 1 : (int)(_downloadOperation.Progress * _currentUpdatingBundle.Value.Size); } }
        
        public int SingleSize { get { return _currentUpdatingBundle.Value == null ? 1 : _currentUpdatingBundle.Value.Size; } }
        
        public long TotalownloadedSize { get { return _writedSize + SingleDownloadedSize; } }
        
        public long TotalSize { get; private set; }

        public int RemainBundleCount
        {
            get { return _updateBundleQueue.Count; }
        }
        
        public bool IsDone { get; private set; }

        private long _writedSize;
        
        private readonly VersionInfo _originVersionInfo;
        private readonly VersionInfo _sourceVersionInfo;

        private readonly Queue<KeyValuePair<SourceType, BundleInfo>> _updateBundleQueue = new Queue<KeyValuePair<SourceType, BundleInfo>>();
        private KeyValuePair<SourceType, BundleInfo> _currentUpdatingBundle;
        
        private DownloadOperation _downloadOperation;

        public UpdateOperation(VersionInfo originVersion, VersionInfo sourceVersion, SourceType souceType)
        {
            _originVersionInfo = originVersion;
            _sourceVersionInfo = sourceVersion;
            if (originVersion.VersionNum < sourceVersion.VersionNum)
            {
                foreach (var sourceBundle in sourceVersion.Bundles)
                {
                    var isContain = false;
                    foreach (var originBundle in originVersion.Bundles)
                    {
                        if (originBundle.Name == sourceBundle.Name)
                        {
                            if (originBundle.Md5Code != sourceBundle.Md5Code)
                            {
                                _updateBundleQueue.Enqueue(new KeyValuePair<SourceType, BundleInfo>(souceType, sourceBundle));
                                TotalSize += sourceBundle.Size;
                            }
                            isContain = true;
                            break;
                        }
                    }
                    if (!isContain)
                    {
                        _updateBundleQueue.Enqueue(new KeyValuePair<SourceType, BundleInfo>(souceType, sourceBundle));
                        TotalSize += sourceBundle.Size;
                    }
                }
            }
        }

        public override bool keepWaiting
        {
            get
            {
                if (_downloadOperation == null)
                {
                    if (_updateBundleQueue.Count > 0)
                    {
                        _currentUpdatingBundle = _updateBundleQueue.Dequeue();
                        _downloadOperation = new DownloadOperation(_currentUpdatingBundle.Key, CalcPath(_currentUpdatingBundle.Value.Name));
                    }
                    else
                    {
                        IsDone = true;
                    }
                }
                else
                {
                    if (_downloadOperation.IsDown)
                    {
                        Utils.Instance.WriteBytesTo(SourceType.PersistentPath, CalcPath(_currentUpdatingBundle.Value.Name), _downloadOperation.Bytes);
                        _originVersionInfo.UpdateBundle(_currentUpdatingBundle.Value);
                        var bytes = System.Text.Encoding.ASCII.GetBytes(JsonMapper.ToJson(_originVersionInfo));
                        Utils.Instance.WriteBytesTo(SourceType.PersistentPath, CalcPath(Settings.VersionFileName), bytes);
                        
                        _writedSize += _downloadOperation.Bytes.Length;
                        
                        if (_updateBundleQueue.Count > 0)
                        {
                            _currentUpdatingBundle = _updateBundleQueue.Dequeue();
                            _downloadOperation = new DownloadOperation(_currentUpdatingBundle.Key, CalcPath(_currentUpdatingBundle.Value.Name));
                        }
                        else
                        {
                            _originVersionInfo.UpdateVersion(_sourceVersionInfo);
                            bytes = System.Text.Encoding.ASCII.GetBytes(JsonMapper.ToJson(_originVersionInfo));
                            Utils.Instance.WriteBytesTo(SourceType.PersistentPath, CalcPath(Settings.VersionFileName), bytes);
                            IsDone = true;
                        }
                    }
                }
                return !IsDone;
            }
        }


        private string CalcPath(string fileName)
        {
            var rootPath = Path.Combine(Settings.RelativePath, Utils.GetBuildPlatform(Application.platform).ToString());
            var path = Path.Combine(rootPath, fileName);
            return path;
        }
    }
}