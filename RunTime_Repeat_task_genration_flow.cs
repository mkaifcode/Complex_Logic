 public class LocationTaskGroup
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int location_task_group_id { get; set; }
		public int location_task_group_fk_location_id { get; set; }
		public int location_task_group_fk_task_group_id { get; set; }
		public int location_task_group_fk_client_id { get; set; }
		public int location_task_group_fk_business_id { get; set; }
		public bool location_task_group_archive { get; set; }
		[Column(TypeName = "json")]
		public object location_task_group_config { get; set; }
		public int location_task_group_created_by { get; set; }
		public DateTime location_task_group_created_at { get; set; }
		public int location_task_group_modified_by { get; set; }
		public DateTime location_task_group_modified_at { get; set; }
		[ForeignKey("location_task_group_fk_task_group_id")]
		public TaskGroup task_group { get; set; }

     // Repeat Details
     public DateTime? location_task_start_date { get; set; }
     public DateTime? location_task_repeat_end_date { get; set; }
     public string location_task_repeat_type { get; set; } = "Daily"; // "weekly", "monthly", "yearly"
     public int location_task_repeat_interval { get; set; } = 1;  // if 1 ==> every repeat_type is weekkly and then ever week // if  2 ==> every repeat_type is weekkly and then ever second week
		public string location_task_repeat_repeat_days { get; set; } = ""; // Daylist concate with - Like Monday-Tuesday
     public int? location_task_repeat_day_of_month { get; set; } // 1 to 31
		public bool location_task_repeat_on_nth_weekday { get; set; } = false; // True false 
		public string location_task_repeat_nth_week { get; set; } = ""; //'first', 'second', 'third', 'fourth', 'fifth'
     public bool location_task_repeat_is_never_end { get; set; } = true;
 } 

public async Task<int> GetLocationTaskGroupCount(int areaId, int locationId, int businessId , DateTime startdate , DateTime? endDate)
{
    var area = await _unitOfWork.Areas.FindAsync(x => !x.area_archive
                                       && x.area_fk_business_id == businessId
                                       && x.area_id == areaId);

    if (area == null || area.area_location_task_enable != true)
    {
        return 0;
    }

    var allTask = await _unitOfWork.LocationTaskGroups.FindAllAsyncNoTracking(x => x.location_task_group_fk_business_id == businessId
                                                                                && x.location_task_group_fk_location_id == locationId
    && x.location_task_group_archive == false);

    var repeatTasks = this.GenerateTaskList(allTask.ToList(), startdate, endDate);

    return repeatTasks == null ? 0 : repeatTasks.Count;
}


#region Repeat Location Task Helper
 public List<LocationTaskGroup> GenerateTaskList(List<LocationTaskGroup> groups, DateTime startDate, DateTime? endDate)
 {
     // Ensure start and end dates are in local time
     startDate = this.EnsureLocalTime(startDate);
     endDate = endDate.HasValue ? this.EnsureLocalTime(endDate.Value) : (DateTime?)null;

     List<LocationTaskGroup> response = new List<LocationTaskGroup>();

     try
     {
         foreach (var group in groups)
         {
             if (group.location_task_start_date.HasValue)
                 group.location_task_start_date = this.EnsureLocalTime(group.location_task_start_date.Value);

             if (group.location_task_repeat_end_date.HasValue)
                 group.location_task_repeat_end_date = this.EnsureLocalTime(group.location_task_repeat_end_date.Value);

             // Process based on repeat type
             switch (group.location_task_repeat_type)
             {
                 case LocationRepeatConstant.REPEAT_TYPE_DAILY:
                     this.GenerateDailyTasks(group, startDate, endDate, response);
                     break;

                 case LocationRepeatConstant.REPEAT_TYPE_WEEKLY:
                     this.GenerateWeeklyTasks(group, startDate, endDate, response);
                     break;

                 case LocationRepeatConstant.REPEAT_TYPE_MONTHLY:
                     this.GenerateMonthlyTasks(group, startDate, endDate, response);
                     break;

                 default:
                     // Handle unknown repeat types if needed
                     break;
             }
         }
     }
     catch (Exception ex)
     {
         _logger.LogWarning($"GenerateTaskList -- {ex.Message}", new { requestData = JsonConvert.SerializeObject(groups),startDate,endDate });
         throw ex;                
         //return response;
     }
     return response;
 }

 // Ensure a DateTime is converted to LocalTime
 private DateTime EnsureLocalTime(DateTime date)
 {
     return date.Kind == DateTimeKind.Local ? date : VMs.Helper.ToLocalTimeGlobal.ToLocalTime(date);
 }

 // Generate tasks for Daily repeat type
 private List<LocationTaskGroup> GenerateDailyTasks(LocationTaskGroup group, DateTime startDate, DateTime? endDate, List<LocationTaskGroup> response)
 {
     DateTime currentDate = startDate;            
     
     if (this.IsWithinRange(group.location_task_start_date.Value, group.location_task_repeat_end_date, currentDate))
     {
         response.Add(group);
     }

     return response;
 }

 // Generate tasks for Weekly repeat type
 private List<LocationTaskGroup> GenerateWeeklyTasks(LocationTaskGroup group, DateTime startDate, DateTime? endDate, List<LocationTaskGroup> response)
 {
     var repeatDays = group.location_task_repeat_repeat_days.Split('-');
     DateTime currentDate = startDate;

     // Single-day scenario: either endDate is null or equal to startDate
     if (!endDate.HasValue || startDate.Date == endDate.Value.Date)
     {
         return this.ProcessWeeklyTaskForDay(group, currentDate, repeatDays, response);                
     }

     // Multi-day scenario: process across a range of dates
     while (currentDate <= endDate.Value)
     {
         response.AddRange(this.ProcessWeeklyTaskForDay(group, currentDate, repeatDays, response));
         currentDate = currentDate.AddDays(1);
     }

     return response;
 }

 // Helper method to process tasks for a single day
 private List<LocationTaskGroup> ProcessWeeklyTaskForDay(LocationTaskGroup group, DateTime currentDate, string[] repeatDays, List<LocationTaskGroup> response)
 {
     int weekCounter = ((int)(currentDate - group.location_task_start_date.Value).TotalDays / 7) + 1;

     if (weekCounter % group.location_task_repeat_interval == 0)
     {
         foreach (var day in repeatDays)
         {
             if (Enum.TryParse(day, true, out DayOfWeek dayOfWeek) &&
                 currentDate.DayOfWeek == dayOfWeek &&
                 this.IsWithinRange(group.location_task_start_date.Value, group.location_task_repeat_end_date, currentDate))
             {
                 response.Add(group);
             }
         }
     }

     return response;
 }

 // Generate tasks for Monthly repeat type
 private List<LocationTaskGroup> GenerateMonthlyTasks(LocationTaskGroup group, DateTime startDate, DateTime? endDate, List<LocationTaskGroup> response)
 {
     DateTime currentDate = startDate;

     // Single-day scenario: either endDate is null or equal to startDate
     if (!endDate.HasValue || startDate.Date == endDate.Value.Date)
     {
         return this.ProcessMonthlyTaskForDay(group, currentDate, response);                
     }

     // Multi-day scenario: process across a range of dates
     while (currentDate <= endDate.Value)
     {
         response.AddRange(this.ProcessMonthlyTaskForDay(group, currentDate, response));
         currentDate = currentDate.AddDays(1);
     }

     return response;
 }

 // Helper method to process tasks for a single day
 private List<LocationTaskGroup> ProcessMonthlyTaskForDay(LocationTaskGroup group, DateTime currentDate, List<LocationTaskGroup> response)
 {
     if (!this.IsWithinRange(group.location_task_start_date.Value, group.location_task_repeat_end_date, currentDate))
     {
         return response;
     }

     if (group.location_task_repeat_on_nth_weekday)
     {
         // Handle nth weekday logic
         if (this.GetNthWeek(currentDate) == this.GetWeekIndex(group.location_task_repeat_nth_week.ToLower()) &&
             this.IsDayOfWeekContain(currentDate.DayOfWeek.ToString(), group.location_task_repeat_repeat_days))
         {
             response.Add(group);
         }
     }
     else if (currentDate.Day == group.location_task_repeat_day_of_month)
     {
         // Handle specific day of the month
         response.Add(group);
     }

     return response;
 }

 // Get the nth week of the month for a given date
 private int GetNthWeek(DateTime date)
 {
     return (date.Day - 1) / 7 + 1;
 }

 // Get the week index based on textual representation
 private int GetWeekIndex(string nthWeek)
 {
     return nthWeek switch
     {
         "first" => 1,
         "second" => 2,
         "third" => 3,
         "fourth" => 4,
         "last" => 5,
         _ => 1
     };
 }

 // Check if the current day is within a list of days
 private bool IsDayOfWeekContain(string currentDay, string listOfDays)
 {
     var repeatDays = listOfDays.Split('-');
     return repeatDays.Any(day => day.Equals(currentDay, StringComparison.OrdinalIgnoreCase));
 }

 // Check if a given current date is within the task's range
 private bool IsWithinRange(DateTime taskStartDate, DateTime? taskEndDate, DateTime currentDate)
 {
     return taskEndDate.HasValue
         ? taskStartDate <= currentDate && currentDate <= taskEndDate.Value
         : taskStartDate <= currentDate;
 }
 #endregion Repeat Location Task Helper


////// Option 2 for lastweek

// Helper method to process tasks for a single day
private List<LocationTaskGroup> ProcessMonthlyTaskForDay(LocationTaskGroup group, DateTime currentDate, List<LocationTaskGroup> response)
{
    if (!this.IsWithinRange(group.location_task_start_date.Value, group.location_task_repeat_end_date, currentDate))
    {
        return response;
    }

    if (group.location_task_repeat_on_nth_weekday)
    {
        // Handle nth weekday logic
        int currentWeek = this.GetNthWeek(currentDate);
        int targetWeek = this.GetWeekIndex(group.location_task_repeat_nth_week.ToLower());

        // Check for "last" week condition
        if (targetWeek == 5)
        {
            // Get the last occurrence of the weekday in the current month
            var lastWeekdays = this.GetLastWeekdaysOfMonth(currentDate.Year, currentDate.Month, group.location_task_repeat_repeat_days);
            if (lastWeekdays.Any(lastWeekday => currentDate.Date == lastWeekday.Date))
            {
                response.Add(group);
            }
        }
        else if (currentWeek == targetWeek &&
                 this.IsDayOfWeekContain(currentDate.DayOfWeek.ToString(), group.location_task_repeat_repeat_days))
        {
            response.Add(group);
        }
    }
    else if (currentDate.Day == group.location_task_repeat_day_of_month)
    {
        // Handle specific day of the month
        response.Add(group);
    }

    return response;
}

// Get the nth week of the month for a given date
private int GetNthWeek(DateTime date)
{
    return (date.Day - 1) / 7 + 1;
}

// Check if 
// Check if Finds the last occurrence of a specific weekday in a given month and year.
private List<DateTime> GetLastWeekdaysOfMonth(int year, int month, string targetDaysOfWeek)
{
    List<DateTime> lastWeekdays = new List<DateTime>();
    DateTime lastDayOfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));

    foreach (var targetDayOfWeek in targetDaysOfWeek.Split('-'))
    {
        DateTime currentDay = lastDayOfMonth;

        while (currentDay.DayOfWeek.ToString() != targetDayOfWeek)
        {
            currentDay = currentDay.AddDays(-1);
        }

        lastWeekdays.Add(currentDay);
    }

    return lastWeekdays;
}
