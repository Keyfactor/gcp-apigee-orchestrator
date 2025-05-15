// Copyright 2024 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
﻿using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Jobs
{
    public class JobBase
    {
        public string ExtensionName => "";

        protected string JsonKey { get; set; }

        public IPAMSecretResolver _resolver;

        internal void SetPAMSecrets(string jsonKey, ILogger logger)
        {
            JsonKey = PAMUtilities.ResolvePAMField(_resolver, logger, "Json Key", jsonKey);
        }
    }
}
