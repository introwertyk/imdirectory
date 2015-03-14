#iMDirectory About


# Introduction #

  * iMDirectory is part of Identity and Access Management framework which supports federated identity based AuthN and AuthZ
  * Synchronizes external directories into central federated identity repository
  * Central repository for all connected directories; meta-data source for migrations, reporting, security audits, etc.
  * Enables corporate directory auditing
  * Stores objects dependencies based on AD DS linking attributes
  * Supports custom object linking as a part of IAM framework
  * Provide reliable identity source for ABAC and RBAC processing
  * Easy to integrate with other Cloud-based services or corporate internal systems
![https://imdirectory.googlecode.com/svn/wiki/SyncGraph.gif](https://imdirectory.googlecode.com/svn/wiki/SyncGraph.gif)
Figure 1. Synchronization flow

# Technical Specification #
Complete Technical Specification can be found here: [iMDirectory](iMDirectory.md).
  * iMDirectory component resides on detached system which is either a Cloud platform or a standalone server
  * Synchronization with directories is a pull operation that is triggered on configurable interval
  * Directory objects are stored with database internal indexes which enables reporting of interconnected objects; This feature enables:
    * Joining objects based on different criteria (custom IAM)
    * Support AD DS linking attributes which are defined as group of interrelated object classes
  * Component resides on MS SQL database, however currently on-going projects aim to enable integration with NoSQL cloud based databases like DynamicDB etc.
  * iMDirectory uses latest .Net Framework techniques introduced in version 4.0 for asynchronous processing, sub-tasks multithreading and memory control
  * Delivered with dedicated libraries:
    * iEngineConnectors: defines different types of external source connectors (AD DS, LDAP, MS SQL)
    * `*`iSecurityComponent: delivers methods for credentials secure handling; supports functionality to integrate with external password vaults
    * iEngineConfiguration: delivers basic component configuration as well as definitions for linking and object classes
> `*`Please note that iSecurityComponent implements encryption functionality as a PoC; Library is still in early development state; Potentially offers such encryption methods as external HSM
All of the libraries can be compiled as one standalone Windows Service or Worker Role to meet custom requirements
  * iMDirectory works in several modes:
    * Two AD DS modes: Domain and Forest
    * One LDAP mode: Domain

# Related Projects #
  * [iCOR3 Project](https://code.google.com/p/icor3/)
  * [iAuthX Project](https://code.google.com/p/iauthx/)
  * [iQu Project](https://code.google.com/p/iqu/)

# Use Cases - Examples #
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

# Disclaimer #
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.