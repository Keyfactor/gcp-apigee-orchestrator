## Overview

Apigee is a Google Cloud Platform (GCP) software product for developing and managing APIs. The remote GCP Apigee 
Orchestrator allows for the remote management of Apigee certificate stores. Inventory and Management functions are 
supported. The Orchestrator performs operations utilizing the Apigee REST API.

## Requirements

### Google Cloud Configuration

#### Create GCP service account
* User must create a service account through the Google Cloud Console that will be used to generate an OAuth 2.0 token when making Apigee API requests 
* Within the Google Cloud Console (`console.cloud.google.com`), select the project for which you will generate the OAuth 2.0 token
* Click on the menu at the top-left and navigate to `APIs & Services`
* Select `Credentials` from the sub-menu
* Create a new Service Account by clicking the `Create Credentials` at the top of the screen and complete the following relevant to your environment:

![](docsource/images/ServiceAccountDetails.gif)
		
##### Service Account Details:
 * Service account name = `Keyfactor-ApigeeAPI`
	 * This can be something to uniquely identify what this service account will be used for
 * Service account ID
 * Service account description
 * Grant this service account access to project
	 *Select role: Quick Access > Basic > Owner
 *Click the "Done" button

![](docsource/images/ServiceAccountPermissions.gif)

#### Create service account key 
* From the "APIs & Services" page, select the service account you just created in the previous step
* Go to the "Keys" tab located across the top of the page
* Click the "Add Key" button in the middle of the page and select the "Create new key" option 
* Make sure the key type selected is a JSON (This is the key you will provide when configuring the cert store as outlined in the following instructions)

![](docsource/images/ServiceAccountJson.gif)

## Test Cases

Case Number|Case Name|Case Description|Overwrite Flag?|Trust Store?|Keystore Exists?|Existing Alias?|Private Key?|Alias Name|KeyStore Name|Expected Results|Passed|Screenshots
------------|---------|----------------|--------------|----------|----------------|--------------|---------------|--------------|------------|-----------|------------|-----------
1|Fresh Add Trust Store|This will test adding a new Alias to a Trust Store that does not have any aliases.|True|True|True|False|False|TC1|KS1|Alias/Certificate will be added.|True|![](docsource/images/TC1Results.gif)
2|Add Additional Add Trust Store|This will test adding a new Alias to a Trust Store that has one Alias Already.|True|True|True|False|False|TC2|KS1|Alias/Certificate will be added.|True|![](docsource/images/TC2Results.gif)
3|Replace Without Overwrite Trust Store|This will test the Overwrite Flag being false during a replace|False|True|True|True|False|TC1|KS1|Error will occur saying "Alias already Exists".|True|![](docsource/images/TC3Results.gif)
4|Replace With Overwrite Trust Store|This will test the Overwrite Flag being true during a replace|True|True|True|True|TC1|False|KS1|Error will occur saying "Renewals are not supported for Trust Stores.".|True|![](docsource/images/TC4Results.gif)
5|Remove From Trust Store |This will test removing an alias from a Trust Store|N/A|True|True|True|False|TC1|KS1|Alias/Certificate will be removed.|True|![](docsource/images/TC5Results.gif)
6|Trust Store Inventory |This will test the inventory of an item from the Trust Store|N/A|True|True|True|False|TC2|KS1|TC2 Alias/Certificate will be Inventoried.|True|![](docsource/images/TC6Results.gif)
7|Fresh Add Keystore |This will test adding a new Alias to a Key Store that does not have any aliases.|True|True|True|False|True|TC7|KS2|TC7 Alias/Certificate will be added to KS2.|True|![](docsource/images/TC7Results.gif)
8|Add Additional Add Keystore, With Overwrite |This will test adding a new Alias to a Key Store that has one Alias Already.|True|False|True|True|True|TC8|KS2|Overwrite flag is set and 'TC8' does not exist in Apigee keystore 'KS2'. Cannot renew an alias if it doesn't exist.|True|![](docsource/images/TC8Results.gif)
9|Add Additional Add Keystore, No Overwrite |This will test adding a new Alias to a Key Store that has one Alias Already.|False|False|True|True|True|TC9|KS2|Overwrite flag not set and 1 alias(es) already exist in Apigee keystore 'KS2'. Only 1 alias per keystore is supported for Add.|True|![](docsource/images/TC9Results.gif)
10|Replace/Renew In Keystore, With Overwrite |This will test replacing an Alias in a Key Store.|True|False|True|True|True|TC7|KS2|TC7 Will be replaced/renewed in KS2.|True|![](docsource/images/TC10Results.gif)
11|Replace/Renew In Keystore, No Overwrite |This will test replacing an Alias in a Key Store without the Overwrite Flag.|False|False|True|True|True|TC7|KS2|Overwrite flag not set and alias 'TC7' already exists in Apigee keystore 'KS2'. If attempting to renew, please check overwrite when scheduling this job.|True|![](docsource/images/TC11Results.gif)
12|Key Store Inventory |This will test the inventory of an item from the Key Store|N/A|False|True|True|True|TC7|KS2|TC7 Alias/Certificate will be Inventoried.|True|![](docsource/images/TC12Results.gif)
13|Remove From Keystore|This will test removing an alias from a Key Store|N/A|False|True|True|True|TC7|KS2|TC7 Alias/Certificate will be removed.|True|![](docsource/images/TC13Results.gif)
14|Certificate without Private Key to Key Store|This will test adding a certificate without a private key to a Keystore|False|False|True|False|False|TC14|KS2|Error Stating Alias 'TC14' does not contain a key and the Apigee store 'KS2' is a keystore. Can only add certs with their key|True|![](docsource/mages/TC14Results.gif)
15|Certificate with Private Key to Trust Store|This will test adding a certificate with a private key to a Trust Store|False|True|True|True|True|TC15|KS1|'TC15' contains a key and the Apigee store 'KS1' is a truststore. Can only add public certs to a truststore.|True|![](docsource/images/TC15Results.gif)
16|Add To Trust Store That Does Not Exist In Apigee|This will test adding a certificate without a private key to a Trust Store that does not exist in Apigee.|False|True|False|False|False|TC16|KS3|Unable to find keystore in Apigee|True|![](docsource/images/TC16Results.gif)
17|Add To Key Store That Does Not Exist In Apigee|This will test adding a certificate with a private key to a Key Store that does not exist in Apigee.|False|False|False|False|True|TC17|KS4|Unable to find keystore in Apigee|True|![](docsource/images/TC17Results.gif)
18|Create Trust Store|This will test creating a Trust Store in Apigee|N/A|True|False|N/A|N/A|TC18|KS3|Trust Store Gets Created In Apigee|True|![](docsource/images/TC18Results.gif)
19|Create Key Store|This will test creating a Key Store in Apigee|N/A|False|False|N/A|N/A|TC17|KS5|Key Store is created in Apigee|True|![](docsource/images/TC19Results.gif)


