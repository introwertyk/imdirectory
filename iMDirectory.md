#iMDirectory


# Introduction #

## Background ##
iMDirectory is a project initiated to fill technology gap for external authentication and authorization where internal directory (MS AD(DS) or LDAP) data is securely exposed into the Cloud service or external provider for Identity and Access Management.

## Design Goals ##
Component was designed as an extension to existing or a core component of newly designed Identity and Access Management systems. It updates underlying MS SQL database with data deltas from directories (MS AD(DS) or LDAP). To start processing retrieved data the component requires dedicated integration with IAM system.
SQL based queries enable powerful complex logic implementation. Unlike in directories SQL meta-data can be efficiently processed for various IAM models. Complex SQL statement can execute advanced processing logic while the same implementation using directory would require several custom LDAP queries with logic implemented on the client side.

iMDirectory is a source of data ready for advanced security audits and complex directory migrations.

# Architecture #
## Introduction ##
iMDirectory was designed to integrate with iCOR3 and iAuthX system. As iAuthX is a framework that consists of a GUI and a WebService, the iMDirectory is a core source of meta-data for AuthN and AuthX operations.

# Data #
## Introduction ##
Directory data is transformed into format, which enables it to be stored in SQL database.
Hierarchical objects structure from directory can't be maintained under SQL, therefore object relationship can be transformed into:
  * Attributes like distinguishedName, which are constructed and represent object location in other directory objects context
  * Linked objects where relationship (e.g. membership, management relation) is represented as table of foreign keys to separately reported objects in different tables

## Schema ##
iMDirectory requires dedicated MS SQL database instance.
### Configuration ###
Inside this instance iMDirectory stores configuration information in specific pre-designed tables (list of tables and DB schema below). This configuration also describes relationship/linking definition (more details can be found below in relationship/linking part).

**iObjectClass**
Defines object classes for different directories/connectors. Classes should correspond to classes existing at an end point.
| **Column** | **Description** |
|:-----------|:----------------|
| iObjectClassID | Primary key identifying Class ID; Foreign key to all tables requiring class identification |
| iConnectorID | Connector ID (Foreign key) of connector that class was defined for |
| ObjectClass | Name of an object class |
| TableContext | Table name where objects for given object class and connector instance are stored |
| Filter | LDAP filter definition that will be used to retrieve only objects that meet filter criteria |
| OtherFilter | (Optional) LDAP filter definition used to retrieve objects from the same class, however in context for their deletion |
| SearchRoot | (Optional) LDAP Base DN where search will start to retrieve objects from defined class |

**iConnector**
Defines instance of a connector and domain Fully Qualified Domain Name for target directory (can be overwritten using custom configuration under iConfiguration).
| **Column** | **Description** |
|:-----------|:----------------|
| iConnectorID | Primary key identifying Connector ID; Foreign key to all tables requiring connector identification |
| iConnectorTypeID | Connector type ID (Foreign key) identifies type of the connector instance based on iConnectorType table definition |
| DomainFQDN | Fully Qualified Domain Name of the target directory |
| iParentConnectorID | Identifies connector, which is targeting a sub-domain to parent connector; required if sub-domain need separate configuration context (MS forest case)  |

**iConnectorType**
Defines different connector types. Different types are native to the iMDirectory service. The LDAP types supported are _Active-Directory_, _AD-DS_, `*`_LDAP_. The types pre-define synchronization model and other directory implementation specific behavior.
| **Column** | **Description** |
|:-----------|:----------------|
| iConnectorTypeID | Primary key ID identifies Connector type ID; Foreign key to all tables using connector type information |
| Name | Name of the Connector type; hardcoded inside iMDirectory component defines connector behavior  |
| Category | (Optional) Identifies category for Connector type; hardcoded inside iMDirectory component; defines connector behavior within given connector type |
| Version | (Optional) Same as Category identifies Connector behavior for given LDAP/MS AD(DS) version |
| Port | TCP port for LDAP connector (can be overwritten using custom configuration under iConfiguration) |
| ProtocolVersion | Version of LDAP required to execute proper low level instructions against LDAP |
| PageSize | LDAP page size for data querying |
| Description | (Optional) Helps identifying connector type definition |

`*`Note that LDAP synchronization implementation is not complete in current 1.0.0.0 iMDirectory release.

**iConfiguration**
Defines additional (soft) connectors configuration. Unlike iConnector table parameters extension does not require DB schema update. Specific connector configuration is located under this table. Minor and major iMDirectory component releases use it to extend configuration functionality.
Configuration uses key/value logic to define parameters (parameters can define themselves using k/v structure).
| **Column** | **Description** |
|:-----------|:----------------|
| iConnectorID | Connector ID (Foreign key) identifies connector context for custom configuration |
| KeyName | Name of the key or parameter  |
| KeyValue | Value of the key or parameter value |

**iLinkingAttribute**
Defines linking attribute, its forward and back-link attributes. Linking attribute defines linking that can have place between two directory objects.
More about Microsoft implementation of linking concept can be found under: [MS Dev Center - Linked Attributes](http://msdn.microsoft.com/en-us/library/windows/desktop/ms677270(v=vs.85).aspx)
| **Column** | **Description** |
|:-----------|:----------------|
| iLinkingAttributID | Primary key ID of linking attribute; Foreign key to all tables using linking attribute |
| ForwardLink | Name of the attribute (mostly MS AD(DS) implementation based) that actually stores information about linked object in directory |
| BackLink | (Optional) Name of the attribute that represents back-link value in directory; Symbolic infromation as forward-link points to LinkedWith attribute at the synchronization level |
| LinkedWith | Attribute that is pointed under forward-link attribute; Used to identify back-linked object; Service implementation method used for linking |
| TableContext | Name of the table where instance of attribute linking will be used to report linking |
| Description | (Optional) Helps identifying linking attribute |

**iLinkingDefinition**
Lists all object classes that are implemented in linking context. Linking can have place between different object types (e.g. user of Object-Class _user_ can be a group member of Object-Class _group_).
More information about how to use linking can be found under [iMDirectory#Relationship/Linking](iMDirectory#Relationship/Linking.md) sub-section.
| **Column** | **Description** |
|:-----------|:----------------|
| iLinkingAttribute | Linking attribute ID (Foreign key) identifying linking context from directory |
| iFwdObjectClass | Object class ID (Foreign key) defined in ObjectClassID table; Points to table where objects with forward-linked attribute are stored |
| iBckObjectClass || Object class ID (Foreign key) defined in ObjectClassID table; Points to table where objects with back-linked attribute are stored|

### Object repository ###
Objects can be grouped using Object-Class object class from directory. Grouping is executed using separation by tables. These object tables require minimal schema grid (dedicated columns to identify objects within other tables context).

**`<`customName`>`**-name should identify object class(es) for synchronization within domain context. E.g. ISSpyraNet\_group
| **Column** | **Description** |
|:-----------|:----------------|
| `_`ObjectID | Unique object ID (Primary key) within table context |
| `_`ObjectClassID | Object class ID (Foreign key) pointing to iObjectClass table |
| `<`attributeName`>` | Any attribute from directory; iMDirectory synchronization engine reports attributes based on column names (except _ObjectID and_ObjectClassID) |

### Relationship/Linking ###
Relationship between different objects from various object classes are represented as relationship of primary keys from different tables. Relationship is identified as linking of attribute because it is derived from object attributes.

**`<`customName`>`** - name should identify linking that is synchronized within some domain context. E.g. ISSpyraNet\_group, where ISSpyraNet is for isspyra.net domain and group for a _group_ class.
| **Column** | **Description** |
|:-----------|:----------------|
| iLinkingAttributeID | Linking attribute ID (Foreign key) defined in iLinkingAttribute table |
| iFwdObjectClassID | Forward link object classs ID (Foreign key); Identifies iFwdObjectID context |
| iLinkingAttributeID | Linking attribute ID (Foreign key) defined in iLinkingAttribute table |
| iFwdObjectClassID | Forward link object classs ID (Foreign key); Identifies iFwdObjectID context |


## Data Format ##
### Directory Attributes ###
iMDirectory synchronization engine casts all attributes to strings and multi-valued attributes into char-separated string (semi-collon is a default separator used for multi-valued attributes; it can be changed using custom configuration).
It is very important to understand exact directory attribute definition before new attribute is added for synchronization. Most of the attributes can be easily casted either to string (varchar) or integer (long/biginteger).

### Integration ###
Simple casting of attribute data types makes integration simple as all complex attributes can be represented by basic data types. Any logic (e.g. from RBAC or ABAC access control models) can be implemented with common SQL statement matching different columns (attributes) with pre-defined or derived patterns.

## Communication ##
iMDirectory uses LDAP protocol to communicate with directories for data retrieval. Internal database uses SQL to control local data changes.
LDAP protocol depends of implementation and custom configuration may use different TCP port numbers (configurable via connector settings). SQL uses default MS SQL remote connection port (1433; configurable via connection string definition).

# Code #
## Introduction ##
The whole project was created under .Net using C# programming language.
The core component was designed out of three main functional parts:
  * Connectors
  * Security
  * Configuration
All of these parts are defined in further sections.

## Modules ##
Modules are organized into following libraries:
  * iEngineConnectors: defines different types of external source connectors (AD DS, LDAP, MS SQL)
  * iSecurityComponent: delivers methods for credentials secure handling; supports functionality to integrate with external password vaults
  * iEngineConfiguration: delivers basic component configuration as well as definitions for linking and object classes
In 1.0.0.0 iMDirectory release all libraries are part of the core program and compile into one Windows Service program file or MS Windows Azure Worker Role.
To make future modifications and extensions more flexible these libraries might have to be changed into standalone libraries and compiled into dll's.

# Operations #
This component is responsible for secure MS AD(DS)/LDAP with MS SQL Database synchronization for further meta-directory processing.
Main component operation consist of:
  * Retrieve latest meta-directory synchronization history record
  * Retrieve ldap data deltas (use meta-directory synchronization history to get only the latest updates)
  * Save latest meta-directory synchronization update into component database

# Scenarios #
## Federate identity across several identity boundaries ##
_Fusion of two or more companies requires centralized authentication system where employees can be authenticated across several security boundaries to federated systems.
iMDirectory can work on domain level and be restricted to only several domain containers and object attributes that are required for federated identity. In this approach authentication can be implemented using claim-based identity. As sub-scope that iMDirectory can access is fully controlled on the component level risk of sensitive data leakage is minimal. This configuration is recommended for all Cloud-based implementations._
![https://imdirectory.googlecode.com/svn/wiki/Example1.gif](https://imdirectory.googlecode.com/svn/wiki/Example1.gif)
Figure 2. AuthN and AuthZ flow - Example

## Migrate or merge AD DS environments ##
_After years of co-existence of several separated internal forests company decides to migrate all directories into one enterprise AD DS forest. iMDirectory as a complete directories repository is a source of migration information, which with relevant logic applied can be used to provision a new forest using legacy directory information._
_As several MS systems rely on AD DS data iMDirectory is an ideal solution for all types of migrations where AD DS was used for meta-data information. Used with custom migration framework eliminates need of purchasing products that can offer only migration functionality for very specific system configurations._
![https://imdirectory.googlecode.com/svn/wiki/Example2.gif](https://imdirectory.googlecode.com/svn/wiki/Example2.gif)
Figure 3. MS AD(DS) Migration - Example

## RBAC via ABAC ##
_Company decides to implement Role-based Access Control model across company internal systems. As LDAP or MS AD DS were used for years for Identity and Access Control there is a requirement to use the framework that can almost transparently integrate with exiting IAM infrastructure.
This component can be used to stream data out to LDAP or AD DS based on defined conditions. If one of the conditions is to allow VPN access to all HR and IT employees the RBAC role can be executed via Attribute-based Access Control. This approach would check specific attribute using custom filter, e.g. department LIKE ‘HR’ OR department LIKE ‘IT’ in order to decide upon VPN security entitlements._
_All of these examples require additional components, which are part of a different framework, although iMDirectory is a core part of each of these frameworks. iMDirectory can be easily integrated with any custom IAM framework._

# Installation #
## Cloud-based ##
### Azure ###
## Local ##
### Pre-requisites ###
### Windows Service ###
### Database ###

# Licensing #
The source code is protected under MIT licensing.
The project was initiated to share experience and indeas related to meta-directory solutions and IAM.
Initially planned to release it as a commercial product, however over time realized it will stay behind other projects that are either well sponsored or have been under open source license for long.

# Upgrades #
1.0.0.0	-	01.09.2013	-	Initial iMDirectory Release

# Uninstallation #
## Cloud-based ##
## Local ##
### Uninstall Windows Service ###
### Decomission meta-directory data ###

# Development #


# Conformance with standards #

# Integration #

# Security #

# Glossary #

# Bibliography #