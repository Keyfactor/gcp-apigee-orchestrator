**Remote GCP Apigee**

**Overview**

Apigee is a Google Cloud Platform GCP software product for developing and managing APIs.  The remote GCP Apigee Orchestrator allows for the remote management of Apigee certificate stores.  Inventory and Management functions are supported.

This agent implements four job types – Inventory, Management Add, Create and Management Remove. Below are the steps necessary to configure this Orchestrator.


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

**2. Register the GCP Apigee Orchestrator with Keyfactor**
See Keyfactor InstallingKeyfactorOrchestrators.pdf Documentation.  Get from your Keyfactor contact/representative.

**3. Create a GCP Apigee Certificate Store within Keyfactor Command**
In Keyfactor Command create a new Certificate Store similar to the one below

![](images/CertStoreSettings.gif)

#### STORE CONFIGURATION 
CONFIG ELEMENT	|DESCRIPTION
----------------|---------------
Category	|The type of certificate store to be configured. Select category based on the display name configured above "GCP Apigee".
Container	|This is a logical grouping of like stores. This configuration is optional and does not impact the functionality of the store.
Client Machine	|The Base URL for the GCP Apigee REST Api. Should be apigee.googleapis.com
Store Path	|This will point to the Apigee keystore that you are managing, and must be provided in the following format, where {org}, {env}, and {keystore} will be replaced with your environment-specific values organizations/{org}/environments/{env}/keystores/{keystore} .
Google Json Key File|Will need updated with the JSON key tied to the Apigee service account. You can copy and paste the entire key Json in the textboxes.
Is Trust Store?|Should be checked if the Apigee keystore being managed is a truststore.
Orchestrator	|This is the orchestrator server registered with the appropriate capabilities to manage this certificate store type. 
Inventory Schedule	|The interval that the system will use to report on what certificates are currently in the store. 
Use SSL	|This should be checked.
User	|This is not necessary.
Password |This is not necessary.

*** 

#### Usage


#### TEST CASES
Case Number|Case Name|Case Description|Overwrite Flag?|Trust Store?|Keystore Exists?|Existing Alias?|Alias Name|KeyStore Name|Expected Results|Passed
------------|---------|----------------|--------------|----------|----------------|--------------|---------------|------------|-----------|------------
1|Fresh Add Trust Store|This will test adding a new Alias to a Keystore that does not have any aliases.|True|True|True|False|TC1|KS1|Alias/Certificate will be added.|True
2|Add Additional Add Trust Store|This will test adding a new Alias to a Keystore that has one Alias Already.|True|True|True|False|TC2|KS1|Alias/Certificate will be added.|True
3|Replace Without Overwrite Trust Store|This will test the Overwrite Flag being false during a replace|False|True|True|True|TC1|KS1|Error will occur saying "Alias already Exists".|True
4|Replace With Overwrite Trust Store|This will test the Overwrite Flag being false during a replace|True|True|True|True|TC1|KS1|Error will occur saying "Renewals are not supported for Trust Stores.".|True
5|Remove From Trust Store |This will test removing an alias from a Trust Store|N/A|True|True|True|TC1|KS1|Alias/Certificate will be removed.|True
6|Trust Store Inventory |This will test the inventory of an item from the Trust Store|N/A|True|True|True|TC2|KS1|TC2 Alias/Certificate will be Inventoried.|True

