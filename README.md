# Remote GCP Apigee

Apigee is a Google Cloud Platform (GCP) software product for developing and managing APIs. The remote GCP Apigee AnyAgent allows for the remote management of Apigee certificate stores. Inventory and Management functions are supported. The AnyAgent performs operations utilizing the Apigee REST API.

#### Integration status: Production - Ready for use in production environments.

## About the Keyfactor Universal Orchestrator Capability

This repository contains a Universal Orchestrator Capability which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Capabilities, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Capability, see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Capability plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.

---



## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |  |
|Supports Management Remove|&check; |  |
|Supports Create Store|&check; |  |
|Supports Discovery|  |  |
|Supports Renrollment|  |  |
|Supports Inventory|&check; |  |



---

**Remote GCP Apigee**

**Overview**

Apigee is a Google Cloud Platform GCP software product for developing and managing APIs.  The remote GCP Apigee Orchestrator allows for the remote management of Apigee certificate stores.  Inventory and Management functions are supported.

This agent implements four job types – Inventory, Management Add, Create and Management Remove. Below are the steps necessary to configure this Orchestrator.


***
**Google Cloud Configuration**

1. Read up on Google Cloud Provider Apigee and how it works.

	*User must create a service account through the Google Cloud Console that will be used to generate an OAuth 2.0 token when making Apigee API requests 

		*Once the Google project is selected, click on the menu on the left and go to APIs & Services 

		*Click on Credentials 

		*Create new Service Account 

	*If creating credentials for the first time, will need to Configure Consent Screen 

		*Not sure if applicable to Service Account, only Client IDs 

			*Settings Applied: 

			*Service account name = Keyfactor-ApigeeAPI 

			*Service account description 

			*Select role:	 

				*Quick Access > Basic > Owner 

				*Click Done 

	*Create service account key 

		*Select the service account 

		*Go to Keys 

		*Add key > Create new key 

		*Make sure the key is a JSON 

![](images/Google-Cloud Apigee Api.gif)
![](images/ServiceAccountDetails.gif)
![](images/ServiceAccountPermissions.gif)
![](images/ServiceAccountJson.gif)
*** 
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
Case Number|Case Name|Case Description|Overwrite Flag?|Trust Store?|Keystore Exists?|Existing Alias?|Private Key?|Alias Name|KeyStore Name|Expected Results|Passed|Screenshots
------------|---------|----------------|--------------|----------|----------------|--------------|---------------|--------------|------------|-----------|------------|-----------
1|Fresh Add Trust Store|This will test adding a new Alias to a Trust Store that does not have any aliases.|True|True|True|False|False|TC1|KS1|Alias/Certificate will be added.|True|![](images/TC1Results.gif)
2|Add Additional Add Trust Store|This will test adding a new Alias to a Trust Store that has one Alias Already.|True|True|True|False|False|TC2|KS1|Alias/Certificate will be added.|True|![](images/TC2Results.gif)
3|Replace Without Overwrite Trust Store|This will test the Overwrite Flag being false during a replace|False|True|True|True|False|TC1|KS1|Error will occur saying "Alias already Exists".|True|![](images/TC3Results.gif)
4|Replace With Overwrite Trust Store|This will test the Overwrite Flag being true during a replace|True|True|True|True|TC1|False|KS1|Error will occur saying "Renewals are not supported for Trust Stores.".|True|![](images/TC4Results.gif)
5|Remove From Trust Store |This will test removing an alias from a Trust Store|N/A|True|True|True|False|TC1|KS1|Alias/Certificate will be removed.|True|![](images/TC5Results.gif)
6|Trust Store Inventory |This will test the inventory of an item from the Trust Store|N/A|True|True|True|False|TC2|KS1|TC2 Alias/Certificate will be Inventoried.|True|![](images/TC6Results.gif)
7|Fresh Add Keystore |This will test adding a new Alias to a Key Store that does not have any aliases.|True|True|True|False|True|TC7|KS2|TC7 Alias/Certificate will be added to KS2.|True|![](images/TC7Results.gif)
8|Add Additional Add Keystore, With Overwrite |This will test adding a new Alias to a Key Store that has one Alias Already.|True|False|True|True|True|TC8|KS2|Overwrite flag is set and 'TC8' does not exist in Apigee keystore 'KS2'. Cannot renew an alias if it doesn't exist.|True|![](images/TC8Results.gif)
9|Add Additional Add Keystore, No Overwrite |This will test adding a new Alias to a Key Store that has one Alias Already.|False|False|True|True|True|TC9|KS2|Overwrite flag not set and 1 alias(es) already exist in Apigee keystore 'KS2'. Only 1 alias per keystore is supported for Add.|True|![](images/TC9Results.gif)
10|Replace/Renew In Keystore, With Overwrite |This will test replacing an Alias in a Key Store.|True|False|True|True|True|TC7|KS2|TC7 Will be replaced/renewed in KS2.|True|![](images/TC10Results.gif)
11|Replace/Renew In Keystore, No Overwrite |This will test replacing an Alias in a Key Store without the Overwrite Flag.|False|False|True|True|True|TC7|KS2|Overwrite flag not set and alias 'TC7' already exists in Apigee keystore 'KS2'. If attempting to renew, please check overwrite when scheduling this job.|True|![](images/TC11Results.gif)
12|Key Store Inventory |This will test the inventory of an item from the Key Store|N/A|False|True|True|True|TC7|KS2|TC7 Alias/Certificate will be Inventoried.|True|![](images/TC12Results.gif)
13|Remove From Keystore|This will test removing an alias from a Key Store|N/A|False|True|True|True|TC7|KS2|TC7 Alias/Certificate will be removed.|True|![](images/TC13Results.gif)
14|Certificate without Private Key to Key Store|This will test adding a certificate without a private key to a Keystore|False|False|True|False|False|TC14|KS2|Error Stating Alias 'TC14' does not contain a key and the Apigee store 'KS2' is a keystore. Can only add certs with their key|True|![](images/TC14Results.gif)
15|Certificate with Private Key to Trust Store|This will test adding a certificate with a private key to a Trust Store|False|True|True|True|True|TC15|KS1|'TC15' contains a key and the Apigee store 'KS1' is a truststore. Can only add public certs to a truststore.|![](images/TC15Results.gif)
16|Add To Trust Store That Does Not Exist In Apigee|This will test adding a certificate without a private key to a Trust Store that does not exist in Apigee.|False|True|False|False|False|TC16|KS3|Unable to find keystore in Apigee|True|![](images/TC16Results.gif)
17|Add To Key Store That Does Not Exist In Apigee|This will test adding a certificate with a private key to a Key Store that does not exist in Apigee.|False|False|False|False|True|TC17|KS4|Unable to find keystore in Apigee|True|![](images/TC17Results.gif)
18|Create Trust Store|This will test creating a Trust Store in Apigee|N/A|True|False|N/A|N/A|TC18|KS3|Trust Store Gets Created In Apigee|True|![](images/TC18Results.gif)
19|Create Key Store|This will test creating a Key Store in Apigee|N/A|False|False|N/A|N/A|TC17|KS5|Key Store is created in Apigee|True|![](images/TC19Results.gif)


