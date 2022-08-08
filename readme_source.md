**Remote GCP Apigee**

**Overview**

The GCP Certificate Manager Orchestrator remotely manages certificates on the Google Cloud Platform Certificate Manager Product

This agent implements three job types – Inventory, Management Add, and Management Remove. Below are the steps necessary to configure this AnyAgent.  It supports adding certificates with or without private keys.


**Google Cloud Configuration**

1. Read up on Google Cloud Provider Apigee and how it works.
2. A Google Service Account is needed with the following permissions (Note: Workload Identity Management Should be used but at the time of the writing it was not available in the .net library yet)

3. The following Api Access is needed:

4. Dowload the Json Credential file as shown below:


**1. Create the New Certificate Store Type for the GCP Apigee Orchestrator**

In Keyfactor Command create a new Certificate Store Type similar to the one below:

#### STORE TYPE CONFIGURATION
SETTING TAB  |  CONFIG ELEMENT	| DESCRIPTION
------|-----------|------------------
Basic |Name	|Descriptive name for the Store Type.  Google Cloud Provider Apigee can be used.
Basic |Short Name	|The short name that identifies the registered functionality of the orchestrator. Must be GcpApigee
Basic |Custom Capability|Unchecked
Basic |Job Types	|Inventory, Add, Create and Remove are the supported job types. 
Basic |Needs Server	|Must be checked
Basic |Blueprint Allowed	|Unchecked
Basic |Requires Store Password	|Determines if a store password is required when configuring an individual store.  This must be unchecked.
Basic |Supports Entry Password	|Determined if an individual entry within a store can have a password.  This must be unchecked.
Advanced |Store Path Type| Determines how the user will enter the store path when setting up the cert store.  Freeform
Advanced |Supports Custom Alias	|Determines if an individual entry within a store can have a custom Alias.  This must be Required
Advanced |Private Key Handling |Determines how the orchestrator deals with private keys.  Optional
Advanced |PFX Password Style |Determines password style for the PFX Password. Default
Custom Fields|Is Trust Store?|Name:IsTrustStore Display Name:Is Trust Store Type:String Default Value:None Required:True
Custom Fields|Google Json Key File|Name:Google Json Key File Display Name:jsonKey Type:String Default Value:N/A Required:True
Entry Parameters|N/A| There are no Entry Parameters

**Basic Settings:**

![](images/CertStoreType-Basic.gif)

**Advanced Settings:**

![](images/CertStoreType-Advanced.gif)

**Custom Fields:**

![](images/CertStoreType-CustomFields.gif)
![](images/CertStoreType-CustomFields1.gif)
![](images/CertStoreType-CustomFields2.gif)

**Entry Params:**

![](images/CertStoreType-EntryParameters.gif)

**2. Register the GCP Certificate Manager Orchestrator with Keyfactor**
See Keyfactor InstallingKeyfactorOrchestrators.pdf Documentation.  Get from your Keyfactor contact/representative.

**3. Create a GCP Certificate Manager Certificate Store within Keyfactor Command**
In Keyfactor Command create a new Certificate Store similar to the one below

![](images/CertStoreSettings-1.gif)
![](images/CertStoreSettings-2.gif)
![](images/GoogleCloudProjectInfo.gif)

#### STORE CONFIGURATION 
CONFIG ELEMENT	|DESCRIPTION
----------------|---------------
Category	|The type of certificate store to be configured. Select category based on the display name configured above "GCP Certificate Manager".
Container	|This is a logical grouping of like stores. This configuration is optional and does not impact the functionality of the store.
Client Machine	|The name of the Google Certificate Manager Credentials File.  This file should be stored in the same directory as the Orchestrator binary.  Sample is "favorable-tree-346417-feb22d67de35.json".
Store Path	|This will be the ProjectId of the Google Cloud Project.  Sample here is "favorable-tree-346417".  See above image.
Location|global is the default but could be another region based on the project.
Project Number| As shown in the above image, this can be obtained from the project information in Google Cloud.
Orchestrator	|This is the orchestrator server registered with the appropriate capabilities to manage this certificate store type. 
Inventory Schedule	|The interval that the system will use to report on what certificates are currently in the store. 
Use SSL	|This should be checked.
User	|This is not necessary.
Password |This is not necessary.

*** 

#### Usage

**Adding New Certificate No Map Entry**

![](images/AddCertificateNoMapEntry.gif)

*** 

**Adding New Certificate With Map Entry**

![](images/AddCertificateWithMapEntry.gif)

*** 

**Replace Certficate With Map Entry**

![](images/ReplaceCertificateMapEntry.gif)

*** 

**Replace Certficate No Map Entry**

![](images/ReplaceCertificateNoMapEntry.gif)

*** 

**Replace Certficate With Map Entry**

![](images/ReplaceCertificateMapEntry.gif)

*** 

**Replace Certficate No Map Entry**

![](images/ReplaceCertificateNoMapEntry.gif)

***

**Remove Certificate Map Entry**

![](images/RemoveCertifcateMapEntry.gif)

*** 

**Remove Certficate No Map Entry**

![](images/RemoveCertificateNoMapEntry.gif)


#### TEST CASES
Case Number|Case Name|Case Description|Overwrite Flag|Alias Name|Expected Results|Passed
------------|---------|----------------|--------------|----------|----------------|--------------
1|Fresh Add with New Map and Entry|Will create new map, map entry and cert|False|map12/mentry12/cert12|New Map will be created, New Map Entry Created, New Cert Created|True

