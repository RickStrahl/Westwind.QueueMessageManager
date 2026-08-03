using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Westwind.Utilities;
using Westwind.Utilities.Configuration;

namespace Westwind.MessageQueueing
{    


    public class QueueMessageManagerConfiguration : AppConfiguration
    {
        /// <summary>
        /// The connection string or connection string name
        /// that is used for database access from the 
        /// Queue Manager
        /// </summary>
        public string ConnectionString { get; set; }


        /// <summary>
        /// If true the database will be created automatically if it doesn't exist.
        /// Adds a little overhead on creation of each manager instance.
        /// </summary>
        public bool AutoCreateTables { get; set; } = false;

        /// <summary>
        /// Poll interval for the controller in milliseconds
        /// when no requests are pending
        /// </summary>
        public int WaitInterval { get; set; }

        /// <summary>
        /// The default queue name that is assigned to queues if 
        /// no value is assigned. Defaults to null/empty (ie. no name)
        /// 
        /// This value is assigned to the QueueName property of the manager
        /// </summary>
        public string DefaultQueueName { get; set; }


        /// <summary>
        /// Specifies the default queue to look for
        /// </summary>
        public string DefaultControllerQueueName { get; set; }

        /// <summary>
        /// The number of threads that the Queue controller
        /// uses to process incoming queue requests
        /// </summary>
        public int DefaultThreadCount { get; set; }

       

        /// <summary>
        /// A list of controllers that can be launched automatically
        /// when QueueControllerMultiple is started. Note this
        /// property is available only to the multi-controller implementation.
        /// Otherwise use the QueueName and WaitInterval properties
        /// to modify operation of an individual queue.
        /// </summary>
        public List<ControllerConfiguration> Controllers { get; set; }

        /// <summary>
        /// The URL where SignalR accepts requests on.
        /// Typically this will be ~/signalr
        /// </summary>
        public string MonitorSignalRHubUrl { get; set; }

        /// <summary>
        /// The URL to the Monitor's HTML Page that 
        /// displays the monitor. ~/QueueMonitor.cshtml
        /// </summary>
        public string MonitorHtmlUrl { get; set;  }

        /// <summary>
        /// When self-hosting as a Service you can optionnally 
        /// host the SignalR Service to feed the Monitor Web
        /// interface from the service.
        /// Typically: http://*:8080/ or http://144.12.121.1:8080/
        /// </summary>
        public string MonitorHostUrl { get; set; }


        /// <summary>
        /// Link displayed on the Queue Monitor page that links
        /// back to an external URL on the Host site.
        /// </summary>
        public string MonitorReferringSiteUrl { get; set; }


        /// <summary>
        /// Singleton instance of a Configuration Manager.
        /// Can be used globally to access a single 
        /// Queue Configuration.
        /// Used only by Client when 
        /// </summary>
        public static QueueMessageManagerConfiguration Current { get; private set; }
        

        public QueueMessageManagerConfiguration()
        {
            ConnectionString =  "Server=.;Database=QueueMessageManager;integrated security=true;Enlist=True;MultipleActiveResultSets=True;Encrypt=False";
            WaitInterval = 1000;
            DefaultThreadCount = 1;
            DefaultControllerQueueName = string.Empty;
            MonitorHostUrl = "http://*:5080/";
            MonitorSignalRHubUrl = "~/signalR";
            MonitorHtmlUrl = "~/QueueMonitor.cshtml";
            Controllers = [];
        }


        static QueueMessageManagerConfiguration()
        {
            Current = new QueueMessageManagerConfiguration();
            Current.Initialize();
        }

        /// <summary>
        /// Creates a new instance of a configuration object that's
        /// copied from the stock configuration.
        /// 
        /// Use this if you need to create multiple configurations
        /// for multiple Controllers running at the same time.
        /// </summary>
        /// <returns></returns>
        public static QueueMessageManagerConfiguration CreateConfiguration()
        {
            var manager = new QueueMessageManagerConfiguration();
            DataUtils.CopyObjectData(Current, manager);
            return manager;
        }

        protected override IConfigurationProvider OnCreateDefaultProvider(string fileName, object configData)
        {

           var jsonFile = "qmm-config.json";
            
            var provider = new JsonFileConfigurationProvider<QueueMessageManagerConfiguration>()
            {
                JsonConfigurationFile = jsonFile,                                   
            };            
            
            return provider;
        }



        #region AppConfiguration Values

        #region Scheduler Configs

        /// <summary>
        /// Used to turn on and off the scheduler engine
        /// </summary>
        public bool UseTaskManager { get; set; }

        /// <summary>
        /// Flag for if the Rescheduler is enabled
        /// </summary>
        public bool UseRescheduler { get; set; }
        public string CS_Rescheduler { get; set; }

        /// <summary>
        /// Flag for using Online Enrollment Pending Status Notifications
        /// </summary>
        public bool UseOEPendingStatusNotification { get; set; }
        public string CS_OEPendingStatus { get; set; }

        /// <summary>
        /// Flag for using Online Enrollment Coverage Request Report
        /// </summary>
        public bool UseProcessOECoverageRequestReport { get; set; }
        public string CS_OECoverageRequestReport { get; set; }

        /// <summary>
        /// Flag for Processing Wellness Virta Files
        /// </summary>
        public bool UseProcessWellnessVirtaFiles { get; set; }
        public string CS_WellnessVirta { get; set; }

        #endregion

        #region FTP Settings
        public string SecureBlackBoxLicenseKey { get; set; }
        public string FTPVertaRemoteFolder { get; set; }
        public string FTPVertaLocalFolder { get; set; }
        #endregion

        #region UHC Files
        public string UHCFilePath { get; set; }
        public string UHCRetireesForUHC { get; set; }
        public string UHCRetireesForUHCext { get; set; }
        public string UHCRetireesForUHCFile { get; set; }
        #endregion

        /// <summary>
        /// Flag for Processing Retirees for UHC data
        /// </summary>
        public bool UseProcessRetireesForUHC { get; set; }
        public string CS_RetireesForUHC { get; set; }

        /// <summary>
        /// Flag for Processing Origami Claims
        /// </summary>
        public bool UseProcessOrigamiClaims { get; set; }
        public string CS_OrigamiClaims { get; set; }
        public string FTPOrigamiClaimsRemoteFolder { get; set; }
        public string FTPOrigamiClaimsLocalFolder { get; set; }
        public string FTPOrigamiClaimsPrivateKeyringPath { get; set; }
        public string FTPOrigamiClaimsPublickKeyringPath { get; set; }
        public string FTPOrigamiClaimsPassPhrase { get; set; }

        /// <summary>
        /// New Origami Claims to BCBS
        /// </summary>
        public string FTPOrigamiToBCBSRemoteFolder { get; set; }
        public string FTPOrigamiToBCBSLocalFolder { get; set; }
        public string FTPOrigamiToBCBSLocalFolderArchive { get; set; }
        public string FTPOrigamiToBCBSFile { get; set; }

        /// <summary>
        /// Flag for Processing BCBS Paid Claims
        /// </summary>
        public bool UseProcessBCBSPaidClaims { get; set; }
        public string CS_BCBSPaidClaims { get; set; }
        public string FTPBCBSPaidClaimsRemoteFolder { get; set; }
        public string FTPBCBSPaidClaimsLocalFolder { get; set; }
        public string FTPBCBSPaidClaimsLocalFolderArchive { get; set; }
        public string FTPBCBSPaidClaimsFile { get; set; }

        /// <summary>
        /// Flag for Processing Retro Cancellations
        /// </summary>
        public bool UseProcessRetroCancellations { get; set; }
        public string CS_RetroCancellations { get; set; }
        public string RetroCancellationsFilePath { get; set; }
        public string RetroCancellationsFileName { get; set; }
        public string RetroCancellationsFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Marriage File To ADPH
        /// </summary>
        public bool UseProcessMarriageFileToADPH { get; set; }
        public string CS_MarriageFileToADPH { get; set; }
        public string MarriageFileToADPHFilePath { get; set; }
        public string MarriageFileToADPHFileName { get; set; }
        public string MarriageFileToADPHFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Divorce File From ADPH
        /// </summary>
        public bool UseProcessDivorceFileFromADPH { get; set; }
        public string CS_DivorceFileFromADPH { get; set; }
        public string DivorceFileFromADPHFilePath { get; set; }

        /// <summary>
        /// Flag for Processing Active Member File To ADPH
        /// </summary>
        public bool UseProcessActiveMemberFileToADPH { get; set; }
        public string CS_ActiveMemberFileToADPH { get; set; }
        public string ActiveMemberFileToADPHFilePath { get; set; }
        public string ActiveMemberFileToADPHFileName { get; set; }
        public string ActiveMemberFileToADPHFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Death File From ADPH
        /// </summary>
        public bool UseProcessDeathFileFromADPH { get; set; }
        public string CS_DeathFileFromADPH { get; set; }
        public string DeathFileFromADPHFilePath { get; set; }


        /// <summary>
        /// Flag for Processing Wellness File to Active Health
        /// </summary>
        public bool UseProcessWellnessFileToActiveHealth { get; set; }
        public string CS_WellnessFileToActiveHealth { get; set; }
        public string WellnessFileToActiveHealthFilePath { get; set; }
        public string WellnessFileToActiveHealthFileName { get; set; }
        public string WellnessFileToActiveHealthFileExt { get; set; }
        public string FTPWellnessFileToActiveHealthRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Processing Medicare File To UHC
        /// </summary>
        public bool UseProcessMedicareFileToUHC { get; set; }
        public string CS_MedicareFileToUHC { get; set; }
        public string MedicareFileToUHCFilePath { get; set; }
        public string MedicareFileToUHCFileName { get; set; }
        public string MedicareFileToUHCFileExt { get; set; }
        public string FTPMedicareFileToUHCRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Processing Medicare File To UHC Full (Monthly)
        /// </summary>
        public bool UseProcessMedicareFileToUHCFull { get; set; }
        public string CS_MedicareFileToUHCFull { get; set; }
        public string MedicareFileToUHCFullFilePath { get; set; }
        public string MedicareFileToUHCFullFileName { get; set; }
        public string MedicareFileToUHCFullFileExt { get; set; }
        public string FTPMedicareFileToUHCFullRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Processing Medicare File To UHC AgeIn (Annual)
        /// </summary>
        public bool UseProcessMedicareFileToUHCAgeInAnnual { get; set; }
        public string CS_MedicareFileToUHCAgeInAnnual { get; set; }
        public string MedicareFileToUHCAgeInAnnualFilePath { get; set; }
        public string MedicareFileToUHCAgeInAnnualFileName { get; set; }
        public string MedicareFileToUHCAgeInAnnualFileExt { get; set; }
        public string FTPMedicareFileToUHCAgeInAnnualRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Processing Medicare File To UHC AgeIn (Month)
        /// </summary>
        public bool UseProcessMedicareFileToUHCAgeInMonth { get; set; }
        public string CS_MedicareFileToUHCAgeInMonth { get; set; }
        public string MedicareFileToUHCAgeInMonthFilePath { get; set; }
        public string MedicareFileToUHCAgeInMonthFileName { get; set; }
        public string MedicareFileToUHCAgeInMonthFileExt { get; set; }
        public string FTPMedicareFileToUHCAgeInMonthRemoteFolder { get; set; }
        public string MedicareAgeInMonthLetterSavePath { get; set; }
        public string MedicareAgeInMonthLetterFilePath { get; set; }

        /// <summary>
        /// Flag for Processing Medicare File To UHC Unit Transfer
        /// </summary>
        public bool UseProcessMedicareFileToUHCUnitTransfer { get; set; }
        public string CS_MedicareFileToUHCUnitTransfer { get; set; }
        public string MedicareFileToUHCUnitTransferFilePath { get; set; }
        public string MedicareFileToUHCUnitTransferFileName { get; set; }
        public string MedicareFileToUHCUnitTransferFileExt { get; set; }
        public string FTPMedicareFileToUHCUnitTransferRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Processing Cancellation Notice Reminder
        /// </summary>
        public bool UseProcessCancellationNoticeReminder { get; set; }
        public string CS_CancellationNoticeReminder { get; set; }

        /// <summary>
        /// Flag for Processing Claims Analysis
        /// </summary>
        public bool UseProcessClaimsAnalysis { get; set; }
        public string CS_ClaimsAnalysis { get; set; }
        public string FTPClaimsAnalysisRemoteFolder { get; set; }
        public string ClaimsAnalysisTransferFilePath { get; set; }
        public string ClaimsAnalysisTransferFileName { get; set; }
        public string ClaimsAnalysisTransferFileNameAppended { get; set; }
        public string ClaimsAnalysisTransferFileExt { get; set; }
        public string ClaimsAnalysisPublicKeyringPath { get; set; }
        public string ClaimsAnalysisPrivateKeyringPath { get; set; }


        /// <summary>
        /// Flag for Processing Optum
        /// </summary>
        public string FTPOptumFileRemoteFolder { get; set; }
        public string OptumTransferFileExt { get; set; }
        public string OptumPublicKeyringPath { get; set; }
        public string OptumPrivateKeyringPath { get; set; }

        /// <summary>
        /// Flag for Processing Optum Nightly
        /// </summary>
        public bool UseProcessOptumNightly { get; set; }
        public string CS_OptumNightly { get; set; }
        public string OptumNightlyFilePath { get; set; }
        public string OptumNightlyFileName { get; set; }
        public string OptumNightlyFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Optum Monthly
        /// </summary>
        public bool UseProcessOptumMonthly { get; set; }
        public string CS_OptumMonthly { get; set; }
        public string OptumMonthlyFilePath { get; set; }
        public string OptumMonthlyFileName { get; set; }
        public string OptumMonthlyFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Optum (Active) Mismatch File
        /// </summary>
        public string FTPOptumFileMismatchRemoteFolder { get; set; }
        public bool UseProcessOptumMismatch { get; set; }
        public string CS_OptumMismatch { get; set; }
        public string OptumMismatchFilePath { get; set; }
        public string OptumMismatchFileName { get; set; }
        public string OptumMismatchFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Optum Cancellations (Special Code in TEST)
        /// </summary>
        public string OptumCancellationsFilePath { get; set; }
        public string OptumCancellationsFileName { get; set; }
        public string OptumCancellationsFileExt { get; set; }
        public string OptumCancellationsFileNameInput { get; set; }

        /// <summary>
        /// Flag for Processing Optum Activations (Special Code in TEST)
        /// </summary>
        public string OptumActivationsFilePath { get; set; }
        public string OptumActivationsFileName { get; set; }
        public string OptumActivationsFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Optum Actives (Special Code in TEST)
        /// </summary>
        public string OptumActivesFilePath { get; set; }
        public string OptumActivesFileName { get; set; }
        public string OptumActivesFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Southland
        /// </summary>
        public string FTPSouthlandFileRemoteFolder { get; set; }
        public string SouthlandTransferFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Southland Nightly
        /// </summary>
        public bool UseProcessSouthlandNightly { get; set; }
        public string CS_SouthlandNightly { get; set; }
        public string SouthlandNightlyFilePath { get; set; }
        public string SouthlandNightlyFileName { get; set; }
        public string SouthlandNightlyFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Southland Monthly
        /// </summary>
        public bool UseProcessSouthlandMonthly { get; set; }
        public string CS_SouthlandMonthly { get; set; }
        public string SouthlandMonthlyFilePath { get; set; }
        public string SouthlandMonthlyFileName { get; set; }
        public string SouthlandMonthlyFileExt { get; set; }

        /// <summary>
        /// Flag for Processing Southland (Active) Mismatch File
        /// </summary>
        public string FTPSouthlandFileMismatchRemoteFolder { get; set; }
        public bool UseProcessSouthlandActiveLGMismatch { get; set; }
        public string CS_SouthlandActiveLGMismatch { get; set; }
        public string SouthlandCancellationsFilePath { get; set; }

        /// <summary>
        /// Flag for Processing BCBS Biometric Weekly
        /// </summary>
        public bool UseProcessBCBSBiometricWeekly { get; set; }
        public string CS_BCBSBiometricWeekly { get; set; }
        public string FTPBCBSBiometricWeeklyRemoteFolder { get; set; }
        public string BCBSBiometricWeeklyFilePath { get; set; }
        public string BCBSBiometricWeeklyFileName { get; set; }
        public string BCBSBiometricWeeklyFileExt { get; set; }


        // Comment Remote folder for LGHIP and DENTAL BCBS Nightly
        public string FTPBCBSNightlyRemoteFolder { get; set; }
        public string FTPBCBSNightlyPublicKeyringPath { get; set; }
        public string FTPBCBSNightlyPrivateKeyringPath { get; set; }
        public string FTPBCBSNightlyPassPhrase { get; set; }

        /// <summary>
        /// Flag for Processing BCBS LGHIP Nightly
        /// </summary>
        public bool UseProcessBCBSLGHIPNightly { get; set; }
        public string CS_BCBSLGHIPNightly { get; set; }
        public string BCBSLGHIPNightlyFilePath { get; set; }
        public string BCBSLGHIPNightlyFileName { get; set; }
        public string BCBSLGHIPNightlyFileExt { get; set; }

        /// <summary>
        /// Flag for Processing BCBS Dental Nightly
        /// </summary>
        public bool UseProcessBCBSDentalNightly { get; set; }
        public string CS_BCBSDentalNightly { get; set; }
        public string BCBSDentalNightlyFilePath { get; set; }
        public string BCBSDentalNightlyFileName { get; set; }
        public string BCBSDentalNightlyFileExt { get; set; }


        /// <summary>
        /// Flag for Processing BCBS Screening Import Nightly
        /// </summary>
        public bool UseProcessBCBSScreeningImportNightly { get; set; }
        public string CS_BCBSScreeningImportNightly { get; set; }
        public string FTPBCBSScreeningImportRemoteFolder { get; set; }
        public string FTPBCBSScreeningImportFilePath { get; set; }

        /// <summary>
        /// Flag for Processing BCBS Pharmacy Location Import Weekly
        /// </summary>
        public bool UseProcessBCBSPharmacyLocationImport { get; set; }
        public string CS_BCBSPharmacyLocationImport { get; set; }
        public string FTPBCBSPharmacyLocationImportRemoteFolder { get; set; }
        public string BCBSPharmacyLocationFileName { get; set; }
        public string BCBSPharmacyLocationImportFilePath { get; set; }

        /// <summary>
        /// Flag for Processing BCBS Active to LG Mismatch Weekly
        /// </summary>
        public bool UseProcessBCBSActiveLGMismatch { get; set; }
        public string CS_BCBSActiveLGMismatch { get; set; }
        public string FTPBCBSActiveImportRemoteFolder { get; set; }
        public string BCBSCancellationsImportFilePath { get; set; }

        /// <summary>
        /// Flag for Processing OptumRx Active to LG Mismatch Weekly
        /// </summary>
        public bool UseProcessOptumRxActiveLGMismatch { get; set; }
        public string CS_OptumRxActiveLGMismatch { get; set; }

        /// <summary>
        /// Flag for processing Wellness Participation
        /// </summary>
        public bool UseProcessWellnessParticipation { get; set; }
        public string CS_WellnessParticipation { get; set; }

        /// <summary>
        /// Flag for processing Family No Dependents
        /// </summary>
        public bool UseProcessFamilyNoDependents { get; set; }
        public string CS_FamilyNoDependents { get; set; }

        /// <summary>
        /// Flag for processing EnrollmentSnapshot Monthly
        /// </summary>
        public bool UseEnrollmentSnapshotMonthly { get; set; }
        public string CS_EnrollmentSnapshotMonthly { get; set; }

        /// <summary>
        /// Flag for uploading IQ documents
        /// </summary>
        public bool UseProcessUploadIQDocuments { get; set; }
        public string CS_UploadIQDocuments { get; set; }

        /// <summary>
        /// Flag for Processing ADPH Vaccine Import Nightly
        /// </summary>
        public bool UseProcessADPHVaccineImportNightly { get; set; }
        public string CS_ADPHVaccineImportNightly { get; set; }
        public string FTPADPHVaccineImportRemoteFolder { get; set; }
        public string FTPADPHVaccineImportFilePath { get; set; }

        /// <summary>
        /// Flag for Processing the MCC File 
        /// </summary>
        public bool UseProcessMCCFileToBenefitFocus { get; set; }
        public string CS_MCCFileToBenefitFocus { get; set; }
        public string MCCToBenefitFocusFilePath { get; set; }
        public string MCCToBenefitFocusFileName { get; set; }
        public string MCCToBenefitFocusFileExt { get; set; }
        public string FTPBenefitFocusRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Biometric
        /// </summary>
        public bool UseProcessBiometricDataFileToMerative { get; set; }
        public string CS_BiometricDataFileToMerative { get; set; }
        public string BiometricDataFileToMerativeFilePath { get; set; }
        public string BiometricDataFileToMerativeFileName { get; set; }
        public string BiometricDataFileToMerativeFileExt { get; set; }
        public string FTPMerativeRemoteFolder { get; set; }

        /// <summary>
        /// Flag for Benefitfocus
        /// </summary>
        public bool UseProcessBFMemberChanges { get; set; }
        public string CS_BFMemberChanges { get; set; }
        public string FTPMemberChangesFromBFRemoteFolder { get; set; }
        public string FTPMemberChangesFromBFLocalFolder { get; set; }
        public string MemberChangesSubscriberLetterModularSingleChange { get; set; }
        public string MemberChangesSubscriberLetterModularMultiChange { get; set; }
        public string MemberChangesSubscriberLetterSavePath { get; set; }


        public bool UseProcessBFCensus { get; set; }
        public string CS_BFCensus { get; set; }
        public string FTPBFCensusLocalFolder { get; set; }
        public string FTPBFCensusRemoteFolder { get; set; }


        public bool UseProcessBFBenefit { get; set; }
        public string CS_BFBenefit { get; set; }
        public string FTPBFBenefitRemoteFolder { get; set; }
        public string FTPBFBenefitLocalFolder { get; set; }

        public bool UseProcessBFDependent { get; set; }
        public string CS_BFDependent { get; set; }
        public string FTPBFDependentRemoteFolder { get; set; }
        public string FTPBFDependentLocalFolder { get; set; }

        public bool UseProcessBFMedicare { get; set; }
        public string CS_BFMedicare { get; set; }
        public string FTPBFMedicareRemoteFolder { get; set; }
        public string FTPBFMedicareLocalFolder { get; set; }

        public bool UseProcessBFCOBRA { get; set; }
        public string CS_BFCOBRA { get; set; }
        public string FTPBFCOBRARemoteFolder { get; set; }
        public string FTPBFCOBRALocalFolder { get; set; }

        public string FTPPrimeRemoteFolder { get; set; }
        public string FTPPrimeTARLocalFolder { get; set; }
        public string FTPPrimeMatchbackLocalFolder { get; set; }
        public string FTPPrimeSSHKey { get; set; }

        public string FTPCertifiRemoteFolder { get; set; }
        public string FTPCertifiLocalFolder { get; set; }

        #region SSRS

        /// <summary>
        /// Flag for USAePay Daily Report
        /// </summary>
        public bool UseProcessUSAePayDailyReport { get; set; }
        public string CS_USAePayDailyReport { get; set; }

        /// <summary>
        /// Flag for New Units Past 12Months
        /// </summary>
        public bool UseProcessNewUnitsPast12Months { get; set; }
        public string CS_NewUnitsPast12Months { get; set; }
        public string SSRSNewUnitsPast12MonthsFilePath { get; set; }

        /// <summary>
        /// Flag for Balance Less than Zero
        /// </summary>
        public string SSRSBalanceLessThanZeroFilePath { get; set; }
        public string CS_BalanceLessThanZero { get; set; }
        public bool UseProcessBalanceLessThanZero { get; set; }

        /// <summary>
        /// Flag for Counts of Rate Status
        /// </summary>
        public bool UseProcessCountsOfRateStatus { get; set; }
        public string CS_CountsOfRateStatus { get; set; }
        public string SSRSCountsOfRateStatusFilePath { get; set; }



        /// <summary>
        /// Flag for Previous Month Cancellations
        /// </summary>
        public string SSRSPreviousMonthCancellationsFilePath { get; set; }
        public bool UseProcessPreviousMonthCancellations { get; set; }
        public string CS_PreviousMonthCancellations { get; set; }


        /// <summary>
        /// Flag for Wellness Screening by Provider
        /// </summary>
        public string SSRSWellnessScreeningByProviderFilePath { get; set; }
        public bool UseProcessWellnessScreeningByProvider { get; set; }
        public string CS_WellnessScreeningByProvider { get; set; }

        /// <summary>
        /// Flag for Enrollments Wellness Mismatch
        /// </summary>
        public string SSRSEnrollmentsWellnessMismatchFilePath { get; set; }
        public bool UseProcessEnrollmentsWellnessMismatch { get; set; }
        public string CS_EnrollmentsWellnessMismatch { get; set; }

        /// <summary>
        /// Flag for Contract by Count
        /// </summary>
        public string SSRSContractByCountReportFilePath { get; set; }
        public bool UseProcessContractByCountReport { get; set; }
        public string CS_ContractByCountReport { get; set; }

        /// <summary>
        /// Flag for Weekly Subscriber Address Change Report
        /// </summary>
        public string SSRSWeeklySubscriberAddressChangeReportFilePath { get; set; }
        public bool UseProcessWeeklySubscriberAddressChangeReport { get; set; }
        public string CS_WeeklySubscriberAddressChangeReport { get; set; }


        /// <summary>
        /// Flag for Active Employees on Blue Cross Dental Policy
        /// </summary>
        public string SSRSActiveEmployeesOnBlueCrossDentalPolicyFilePath { get; set; }
        public bool UseProcessActiveEmployeesOnBlueCrossDentalPolicy { get; set; }
        public string CS_ActiveEmployeesOnBlueCrossDentalPolicy { get; set; }


        /// <summary>
        /// Flag for Contract Cancel Open Request
        /// </summary>
        public string SSRSContractCancelOpenRequestFilePath { get; set; }
        public bool UseProcessContractCancelOpenRequest { get; set; }
        public string CS_ContractCancelOpenRequest { get; set; }


        /// <summary>
        /// Flag for Dependent Opt Out Declination Report
        /// </summary>
        public string SSRSDependentOptOutDeclinationReportFilePath { get; set; }
        public bool UseProcessDependentOptOutDeclinationReport { get; set; }
        public string CS_DependentOptOutDeclinationReport { get; set; }


        /// <summary>
        /// Flag for Virta Candidates
        /// </summary>
        public string SSRSVirtaCandidatesFilePath { get; set; }
        public bool UseProcessVirtaCandidates { get; set; }
        public string CS_VirtaCandidates { get; set; }

        /// <summary>
        /// Flag for Wellness Screening Schedule Report
        /// </summary>
        public string SSRSWellnessScreeningScheduleReportFilePath { get; set; }
        public bool UseWellnessScreeningScheduleReport { get; set; }
        public string CS_WellnessScreeningScheduleReport { get; set; }

        /// <summary>
        /// Flag for User Details Monthly
        /// </summary>
        public string SSRSUserDetailsMonthlyFilePath { get; set; }
        public bool UseUserDetailsMonthly { get; set; }
        public string CS_UserDetailsMonthly { get; set; }

        /// <summary>
        /// Flag for New Units
        /// </summary>
        public string SSRSNewUnitsFilePath { get; set; }
        public bool UseProcessNewUnits { get; set; }
        public string CS_NewUnits { get; set; }

        /// <summary>
        /// Flag for Monthly Spouse Wellness Screening Report
        /// </summary>
        public string SSRSMonthlySpouseWellnessScreeningReportFilePath { get; set; }
        public bool UseMonthlySpouseWellnessScreeningReport { get; set; }
        public string CS_MonthlySpouseWellnessScreeningReport { get; set; }


        /// <summary>
        /// Flag for User Details
        /// </summary>
        public string SSRSUserDetailsFilePath { get; set; }
        public bool UseUserDetails { get; set; }
        public string CS_UserDetails { get; set; }

        /// <summary>
        /// Flag for Contract By Count Report Monthly
        /// </summary>
        public string SSRSProcessContractByCountReportMonthlyFilePath { get; set; }
        public bool UseContractByCountReportMonthly { get; set; }
        public string CS_ContractByCountReportMonthly { get; set; }

        /// <summary>
        /// Flag for Wellness Productivity ReportMonthly
        /// </summary>
        public string SSRSQMMWellnessProductivityReportMonthlyFilePath { get; set; }
        public bool UseWellnessProductivityReportMonthly { get; set; }
        public string CS_WellnessProductivityReportMonthly { get; set; }


        /// <summary>
        /// Flag Monthly Contract Activity By Count Report By User
        /// </summary>
        public string SSRSQMMMonthlyContractActivityByCountReportByUserFilePath { get; set; }
        public bool UseMonthlyContractActivityByCountReportByUser { get; set; }
        public string CS_MonthlyContractActivityByCountReportByUser { get; set; }

        #endregion


        /// <summary>
        /// Flag for BF Member Changes Unit Notification
        /// </summary>
        public bool UseProcessBFMemberChangesUnitNotification { get; set; }
        public string CS_BFMemberChangesUnitNotification { get; set; }

        /// <summary>
        /// Flag for BF Member Changes Subscriber Notification
        /// </summary>
        public bool UseProcessBFMemberChangesSubscriberNotification { get; set; }
        public string CS_BFMemberChangesSubscriberNotification { get; set; }

        /// <summary>
        /// Flag for My.LGHIB Password Reset
        /// </summary>
        public bool UseMyLGHIBPasswordReset { get; set; }
        public string CS_MyLGHIBPasswordReset { get; set; }

        /// <summary>
        /// Flag for Member Unit Change Report
        /// </summary>
        public bool UseMemberUnitChangeReport { get; set; }
        public string CS_MemberUnitChangeReport { get; set; }

        /// <summary>
        /// Flag for LGHIB Survey Reminders
        /// </summary>
        public bool UseSurveyReminder { get; set; }
        public string CS_SurveyReminder { get; set; }

        /// <summary>
        /// Flag for LGHIB Survey Reply Emails
        /// </summary>
        public bool UseSurveyReply { get; set; }
        public string CS_SurveyReply { get; set; }

        /// <summary>
        /// Gets or sets the file path to the PDF document requested for documentation purposes.
        /// </summary>
        public string RequestForDocumentationPDF { get; set; }
        public string RequestForDocumentationSavePath { get; set; }



        /// <summary>
        /// LOCAL GOVERNMENT PGP Encryption Keys
        /// </summary>
        public string FTPLGPrivateKeyringPath { get; set; }
        public string FTPLGPublicKeyringPath { get; set; }

        #endregion`
    }

    /// <summary>
    /// Indidual Controller Configuration Item
    /// in a multi-controller configuration.        
    /// </summary>
    public class ControllerConfiguration 
    {
        /// <summary>
        /// Connection string for the database or queue data backend.
        /// </summary>
        public string ConnectionString { get; set; } 

        /// <summary>
        /// Name of the queue - can be empty or null
        /// </summary>
        public string QueueName { get; set; } 

        /// <summary>
        /// Number of threads used for this controller in
        /// threading mode.
        /// </summary>
        public int ControllerThreads { get; set; } = 1;

        /// <summary>
        /// Time to wait between before next request check
        /// </summary>
        public int WaitInterval { get; set; } = 300;


        /// <summary>
        /// The type that is used to create the Queue Message Manager
        /// </summary>
        public string QueueManagerType { get; set; } = nameof(QueueMessageManagerSql);            

        /// <summary>
        /// Allows retrieving an object from a string generated with ToString()
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ControllerConfiguration FromString(string data)
        {
            return StringSerializer.Deserialize<ControllerConfiguration>(data, ",");
        }

        /// <summary>
        /// Creates a serialized string of properties
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return StringSerializer.SerializeObject(this, ",");
        }
    }


    /// <summary>
    /// Determines how timed out messages are handled. Default is Timeout
    /// </summary>
    public enum TimeoutActions
    {
        Timeout,
        Delete,
        Reset,
        Fail,
        Cancel        
    }
}
