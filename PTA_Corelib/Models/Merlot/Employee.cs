using System;
using System.Collections.Generic;

namespace KTI.PAL.Teams.Core.Models.Merlot
{
    /// <summary>
    /// Class object representation for employee data returned by Merlot.
    /// </summary>
    public class Employee : EmployeeBase
    {
        public bool active { get; set; }
        public string birthCity { get; set; }
        public string birthState { get; set; }
        public string birthCountry { get; set; }
        public string concessionCardNumber { get; set; }
        public string crewPortalUserId { get; set; }
        public DateTime dateOfBirth { get; set; }
        public DateTime dateOfJoining { get; set; }
        public DateTime? dateOfTermination { get; set; }
        public string doctor { get; set; }
        public string empCode { get; set; }
        public string employeeStatus { get; set; }
        public string idCardNumber { get; set; }
        public string knownAs { get; set; }
        public string licenseNumber { get; set; }
        public bool? lossOfLicense { get; set; }
        public string maritalStatus { get; set; }
        public string name1 { get; set; }
        public string name2 { get; set; }
        public string name3 { get; set; }
        public string name4 { get; set; }
        public string nationality { get; set; }
        public string nationalityCountry { get; set; }
        public string partnerName { get; set; }
        public string positionCode { get; set; }
        public DateTime? positionDate { get; set; }
        public DateTime? qualifiedUntil { get; set; }
        public string secretQuestion { get; set; }
        public string secretAnswer { get; set; }
        public int? seniority { get; set; }
        public char sex { get; set; }
        public string unionCode { get; set; }

        public List<Address> employeeAddresses { get; set; }
        public List<Attribute> employeeAttributes { get; set; }
        public List<Base> employeeBases { get; set; }
        public List<Department> employeeDepartments { get; set; }
        public List<Document> employeeDocuments { get; set; }
        public List<Email> employeeEmails { get; set; }
        public List<Phone> employeePhones { get; set; }
        public List<Profile> employeeProfiles { get; set; }
        public List<Rating> employeeRatings { get; set; }
    }

    public class Address : EmployeeBase
    {
        public string city { get; set; }
        public string country { get; set; }
        public bool primary { get; set; }
        public string state { get; set; }
        public string streetNoName { get; set; }
        public string suburb { get; set; }
        public string zip { get; set; }
    }

    public class Attribute : EmployeeBase
    {
        public bool active { get; set; }
        public string code { get; set; }
        public string description { get; set; }
        public bool useInOptimizer { get; set; }
    }

    public class Base : EmployeeBase
    {
        public bool accommodation { get; set; }
        public DateTime activeFrom { get; set; }
        public DateTime activeTo { get; set; }
        public bool car { get; set; }
        public string port { get; set; }
        public bool temporary { get; set; }
    }

    public class Department : EmployeeBase
    {
        public DateTime activeFrom { get; set; }
        public DateTime activeTo { get; set; }
        public string assigned { get; set; }
    }

    public class Document : EmployeeBase
    {
        string country { get; set; }
        public string documentType { get; set; }
        public DateTime? expiryDate { get; set; }
        public DateTime? issueDate { get; set; }
        public string issuingAuthority { get; set; }
        public string name1 { get; set; }
        public string name2 { get; set; }
        public string name3 { get; set; }
        public string number { get; set; }
        public string placeOfIssue { get; set; }
        public string type { get; set; }
    }

    public class Email : EmployeeBase
    {
        public string description { get; set; }
        public string email { get; set; }
        public bool primary { get; set; }
    }

    public class Phone : EmployeeBase
    {
        string description { get; set; }
        public string number { get; set; }
        public bool primary { get; set; }
        public bool useForTxt { get; set; }
    }

    public class Profile : EmployeeBase
    {
        public bool active { get; set; }
        public DateTime activeFrom { get; set; }
        public DateTime activeTo { get; set; }
        public string description { get; set; }
        public bool isLanguage { get; set; }
        public string name { get; set; }
        public bool useInOptimizer { get; set; }
    }

    public class Rating : EmployeeBase
    {
        public DateTime activeFrom { get; set; }
        public DateTime activeTo { get; set; }
        public string equipmentGroup { get; set; }
        public string equipmentType { get; set; }
        public string isPrimary { get; set; }
        public bool trainee { get; set; }
        public string rank { get; set; }
    }
}
